using System;
using UnityEngine;

namespace Game.Weather
{
    /// <summary>Small, allocation-free palette. Exposure affects lit terrain AND unlit sprites.</summary>
    internal readonly struct WeatherStyle
    {
        public readonly float Exposure, Saturation, SunStrength, AmbientStrength, FogDensity, Rain;
        public readonly Color Filter, SunColor, FogColor;

        public WeatherStyle(float exposure, float saturation, Color filter, float sunStrength,
            Color sunColor, float ambientStrength, float fogDensity, Color fogColor, float rain = 0f)
        {
            Exposure = exposure;
            Saturation = saturation;
            Filter = filter;
            SunStrength = sunStrength;
            SunColor = sunColor;
            AmbientStrength = ambientStrength;
            FogDensity = fogDensity;
            FogColor = fogColor;
            Rain = rain;
        }

        public static WeatherStyle For(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Sunny:
                    return new WeatherStyle(0f, 0f, Color.white, 1f, Color.white, 1f,
                        0f, new Color(.72f, .81f, .86f));
                case WeatherKind.Cloudy:
                    return new WeatherStyle(-.18f, -18f, new Color(.88f, .94f, 1f),
                        .74f, new Color(.9f, .95f, 1f), .96f, .009f, new Color(.60f, .69f, .76f));
                case WeatherKind.Fog:
                    return new WeatherStyle(.02f, -27f, new Color(.93f, .98f, 1f),
                        .83f, Color.white, 1f, .034f, new Color(.69f, .78f, .80f));
                case WeatherKind.Rain:
                    return new WeatherStyle(-.32f, -20f, new Color(.78f, .88f, 1f),
                        .72f, new Color(.86f, .92f, 1f), .95f, .018f, new Color(.40f, .52f, .63f), 1f);
                case WeatherKind.Sunset:
                    return new WeatherStyle(-.10f, 5f, new Color(1f, .83f, .69f),
                        .95f, new Color(1f, .74f, .51f), .97f, .006f, new Color(.82f, .54f, .40f));
                case WeatherKind.Night:
                    // A readable blue night, not a near-black exposure or a daylight sprite over dark ground.
                    return new WeatherStyle(-.95f, -12f, new Color(.68f, .81f, 1f),
                        .72f, new Color(.72f, .83f, 1f), .87f, .007f, new Color(.14f, .22f, .34f));
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown weather.");
            }
        }

        public static WeatherStyle Lerp(WeatherStyle a, WeatherStyle b, float t) =>
            new WeatherStyle(Mathf.Lerp(a.Exposure, b.Exposure, t), Mathf.Lerp(a.Saturation, b.Saturation, t),
                Color.Lerp(a.Filter, b.Filter, t), Mathf.Lerp(a.SunStrength, b.SunStrength, t),
                Color.Lerp(a.SunColor, b.SunColor, t), Mathf.Lerp(a.AmbientStrength, b.AmbientStrength, t),
                Mathf.Lerp(a.FogDensity, b.FogDensity, t), Color.Lerp(a.FogColor, b.FogColor, t),
                Mathf.Lerp(a.Rain, b.Rain, t));
    }

    /// <summary>Only gameplay seconds advance this state; retargeting starts at the visible palette.</summary>
    internal sealed class WeatherTransition
    {
        WeatherStyle _from = WeatherStyle.For(WeatherKind.Sunny);
        WeatherStyle _target = WeatherStyle.For(WeatherKind.Sunny);
        float _elapsed, _duration;
        public WeatherKind Current { get; private set; } = WeatherKind.Sunny;
        public WeatherStyle Value { get; private set; } = WeatherStyle.For(WeatherKind.Sunny);
        public float Progress => _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
        public bool IsTransitioning => Progress < 1f;

        public bool Set(WeatherKind kind, float duration)
        {
            WeatherStyle target = WeatherStyle.For(kind); // Validate even a repeated enum value.
            if (kind == Current) return false;
            _from = Value;
            _target = target;
            Current = kind;
            _elapsed = 0f;
            _duration = float.IsNaN(duration) || float.IsInfinity(duration) ? 2.4f : Mathf.Max(.01f, duration);
            return true;
        }

        public void Advance(float deltaTime, bool playing)
        {
            if (!playing || !IsTransitioning || deltaTime <= 0f ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            _elapsed = Mathf.Min(_duration, _elapsed + deltaTime);
            float t = Progress;
            Value = WeatherStyle.Lerp(_from, _target, t * t * (3f - 2f * t));
        }

        public void Reset()
        {
            Current = WeatherKind.Sunny;
            Value = _from = _target = WeatherStyle.For(WeatherKind.Sunny);
            _elapsed = _duration = 0f;
        }

        public static WeatherKind Next(WeatherKind current) =>
            current == WeatherKind.Night ? WeatherKind.Sunny : (WeatherKind)((int)current + 1);
    }
}
