using System;
using NUnit.Framework;
using UnityEngine;

namespace Game.Weather.Tests
{
    public sealed class WeatherTransitionTests
    {
        [Test]
        public void CycleVisitsEveryWeatherExactlyOnceAndWrapsToSunny()
        {
            var state = new WeatherTransition();
            var expected = new[] { WeatherKind.Cloudy, WeatherKind.Fog, WeatherKind.Rain,
                WeatherKind.Sunset, WeatherKind.Night, WeatherKind.Sunny };
            foreach (var kind in expected)
            {
                Assert.That(state.Set(WeatherTransition.Next(state.Current), 2f), Is.True);
                Assert.That(state.Current, Is.EqualTo(kind));
            }
        }

        [Test]
        public void TransitionIsGradualAndFinishesAtTheExactTarget()
        {
            var state = new WeatherTransition();
            state.Set(WeatherKind.Night, 2f);
            Assert.That(state.Current, Is.EqualTo(WeatherKind.Night), "HUD reports the requested state.");
            Assert.That(state.Value.Exposure, Is.Zero, "Selecting must not snap the visible world.");
            state.Advance(.5f, true);
            float first = state.Value.Exposure;
            Assert.That(first, Is.LessThan(0f).And.GreaterThan(WeatherStyle.For(WeatherKind.Night).Exposure));
            state.Advance(.5f, true);
            Assert.That(state.Value.Exposure, Is.LessThan(first));
            Assert.That(state.Progress, Is.EqualTo(.5f).Within(.0001f));
            state.Advance(2f, true);
            Assert.That(state.Value.Filter, Is.EqualTo(WeatherStyle.For(WeatherKind.Night).Filter));
            Assert.That(state.Value.Exposure, Is.EqualTo(WeatherStyle.For(WeatherKind.Night).Exposure));
            Assert.That(state.IsTransitioning, Is.False);
        }

        [Test]
        public void RetargetingAnUnfinishedTransitionHasNoVisualDiscontinuity()
        {
            var state = new WeatherTransition();
            state.Set(WeatherKind.Night, 2f);
            state.Advance(.75f, true);
            WeatherStyle visible = state.Value;
            state.Set(WeatherKind.Sunset, 2f);
            Assert.That(state.Value.Exposure, Is.EqualTo(visible.Exposure));
            Assert.That(state.Value.Filter, Is.EqualTo(visible.Filter));
            state.Advance(.01f, true);
            Assert.That(Mathf.Abs(state.Value.Exposure - visible.Exposure), Is.LessThan(.001f));
            state.Advance(2f, true);
            Assert.That(state.Value.Filter, Is.EqualTo(WeatherStyle.For(WeatherKind.Sunset).Filter));
        }

        [Test]
        public void PauseDoesNotConsumeTheRemainingTransitionDuration()
        {
            var state = new WeatherTransition();
            state.Set(WeatherKind.Rain, 2f);
            state.Advance(.5f, true);
            WeatherStyle before = state.Value;
            state.Advance(120f, false);
            Assert.That(state.Value.Rain, Is.EqualTo(before.Rain));
            Assert.That(state.Value.FogDensity, Is.EqualTo(before.FogDensity));
            Assert.That(state.Progress, Is.EqualTo(.25f));
            state.Advance(1f, true);
            Assert.That(state.Progress, Is.EqualTo(.75f));
        }

        [Test]
        public void RepeatedSelectionAndInvalidInputDoNotRestartOrCorruptABlend()
        {
            var state = new WeatherTransition();
            state.Set(WeatherKind.Fog, 2f);
            state.Advance(1f, true);
            Assert.That(state.Set(WeatherKind.Fog, 5f), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Set((WeatherKind)100, 2f));
            foreach (float delta in new[] { -1f, 0f, float.NaN, float.PositiveInfinity })
                state.Advance(delta, true);
            Assert.That(state.Current, Is.EqualTo(WeatherKind.Fog));
            Assert.That(state.Progress, Is.EqualTo(.5f));
            state.Advance(1f, true);
            Assert.That(state.IsTransitioning, Is.False);
        }

        [Test]
        public void ExplicitResetClearsRainImmediatelyEvenWithoutAGameplayTick()
        {
            var state = new WeatherTransition();
            state.Set(WeatherKind.Rain, 1f);
            state.Advance(1f, true);
            Assert.That(state.Value.Rain, Is.EqualTo(1f));
            state.Reset();
            Assert.That(state.Current, Is.EqualTo(WeatherKind.Sunny));
            Assert.That(state.IsTransitioning, Is.False);
            Assert.That(state.Value.Rain, Is.Zero);
            Assert.That(state.Value.Exposure, Is.Zero);
            Assert.That(state.Value.Filter, Is.EqualTo(Color.white));
        }

        [TestCase(WeatherKind.Sunny, "Soleado")]
        [TestCase(WeatherKind.Cloudy, "Nublado")]
        [TestCase(WeatherKind.Fog, "Niebla")]
        [TestCase(WeatherKind.Rain, "Lluvia")]
        [TestCase(WeatherKind.Sunset, "Atardecer")]
        [TestCase(WeatherKind.Night, "Noche")]
        public void HudNamesAreStableAndLocalized(WeatherKind kind, string expected) =>
            Assert.That(WeatherController.DisplayName(kind), Is.EqualTo(expected));
    }
}
