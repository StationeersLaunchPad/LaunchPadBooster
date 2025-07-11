﻿using Assets.Scripts.Atmospherics;
using CharacterCustomisation;
using HarmonyLib;
using System.Reflection.Emit;

namespace LaunchPadBooster.Utils
{
  public static class StringExtensions
  {
    public static string ToStringPrefix(this double value, string unit = "", string color = "") => value.ToStringPrefix(unit, color);
    public static string ToStringPrefix(this float value, string unit = "", string color = "") => value.ToStringPrefix(unit, color);
    public static string ToStringPrefix(this PressurekPa value, string unit = "", string color = "") => value.ToFloat().ToStringPrefix(unit, color);
    public static string ToStringPrefix(this TemperatureKelvin value, string unit = "", string color = "") => value.ToFloat().ToStringPrefix(unit, color);
    public static string ToStringPrefix(this VolumeLitres value, string unit = "", string color = "") => value.ToFloat().ToStringPrefix(unit, color);
    public static string ToStringPrefix(this MoleQuantity value, string unit = "", string color = "") => value.ToFloat().ToStringPrefix(unit, color);
  }

  public static class CodeInstructionExtensions
  {
    public static bool OpcodeIs(this CodeInstruction instruction, OpCode opcode) => instruction?.opcode == opcode;
  }

  public static class UnitExtensions
  {
    public static bool IsKelvinNil(this float value) => value <= Constants.MINIMUM_KELVIN;
    public static bool IsKelvinNil(this TemperatureKelvin value) => value.ToFloat().IsKelvinNil();

    public static bool IsCelciusNil(this float value) => value <= Constants.MINIMUM_CELCIUS;
    public static bool IsCelciusNil(this TemperatureKelvin value) => value.ToFloat().IsCelciusNil();

    public static float CelciusToKelvin(this float celcius) => celcius + Constants.ZERO_CELCIUS_KELVIN;
    public static float CelciusToFahrenheit(this float celcius) => (celcius * 1.8f) + 32f;

    public static float KelvinToCelcius(this float kelvin) => kelvin - Constants.ZERO_CELCIUS_KELVIN;
    public static float KelvinToCelcius(this TemperatureKelvin kelvin) => kelvin.ToFloat().KelvinToCelcius();
    public static float KelvinToFahrenheit(this float kelvin) => (kelvin.KelvinToCelcius() * 1.8f) + 32f;
    public static float KelvinToFahrenheit(this TemperatureKelvin kelvin) => kelvin.ToFloat().KelvinToFahrenheit();

    public static float FahrenheitToCelcius(this float fahrenheit) => (fahrenheit - 32f) * 1.8f;
    public static float FahrenheitToKelvin(this float fahrenheit) => FahrenheitToCelcius(fahrenheit).CelciusToFahrenheit();

    public static float MicroPascalToMilliPascal(this float uPa) => uPa * 1000f;
    public static float MicroPascalToPascal(this float uPa) => uPa.MicroPascalToMilliPascal() * 1000f;
    public static float MicroPascalToKiloPascal(this float uPa) => uPa.MicroPascalToPascal() * 1000f;
    public static float MicroPascalToMegaPascal(this float uPa) => uPa.MicroPascalToKiloPascal() * 1000f;
    public static float MicroPascalToGigaPascal(this float uPa) => uPa.MicroPascalToMegaPascal() * 1000f;

    public static float MilliPascalToMicroPascal(this float mPa) => mPa / 1000f;
    public static float MilliPascalToPascal(this float mPa) => mPa * 1000f;
    public static float MilliPascalToKiloPascal(this float mPa) => mPa.MilliPascalToPascal() * 1000f;
    public static float MilliPascalToMegaPascal(this float mPa) => mPa.MilliPascalToKiloPascal() * 1000f;
    public static float MilliPascalToGigaPascal(this float mPa) => mPa.MilliPascalToGigaPascal() * 1000f;

    public static float PascalToMicoPascal(this float Pa) => Pa.PascalToMilliPascal() * 1000f;
    public static float PascalToMilliPascal(this float Pa) => Pa * 1000f;
    public static float PascalToKiloPascal(this float Pa) => Pa / 1000f;
    public static float PascalToMegaPascal(this float Pa) => Pa.PascalToKiloPascal() / 1000f;
    public static float PascalToGigaPascal(this float Pa) => Pa.PascalToMegaPascal() / 1000f;

    public static float KiloPascalToMicroPascal(this float kPa) => kPa.KiloPascalToMilliPascal() * 1000f;
    public static float KiloPascalToMicroPascal(this PressurekPa kPa) => kPa.ToFloat().KiloPascalToMilliPascal() * 1000f;
    public static float KiloPascalToMilliPascal(this float kPa) => kPa.KiloPascalToPascal() * 1000f;
    public static float KiloPascalToMilliPascal(this PressurekPa kPa) => kPa.ToFloat().KiloPascalToPascal() * 1000f;
    public static float KiloPascalToPascal(this float kPa) => kPa * 1000f;
    public static float KiloPascalToPascal(this PressurekPa kPa) => kPa.ToFloat() * 1000f;
    public static float KiloPascalToMegaPascal(this float kPa) => kPa / 1000f;
    public static float KiloPascalToMegaPascal(this PressurekPa kPa) => kPa.ToFloat() / 1000f;
    public static float KiloPascalToGigaPascal(this float kPa) => kPa.KiloPascalToMegaPascal() / 1000f;
    public static float KiloPascalToGigaPascal(this PressurekPa kPa) => kPa.ToFloat().KiloPascalToMegaPascal() / 1000f;

    public static float MegaPascalToMicroPascal(this float MPa) => MPa.MegaPascalToMilliPascal() / 1000f;
    public static float MegaPascalToMilliPascal(this float MPa) => MPa.MegaPascalToPascal() / 1000f;
    public static float MegaPascalToPascal(this float MPa) => MPa.MegaPascalToKiloPascal() / 1000f;
    public static float MegaPascalToKiloPascal(this float MPa) => MPa / 1000f;
    public static float MegaPascalToGigaPascal(this float MPa) => MPa * 1000f;

    public static float GigaPascalToMicroPascal(this float GPa) => GPa.GigaPascalToMilliPascal() / 1000f;
    public static float GigaPascalToMilliPascal(this float GPa) => GPa.GigaPascalToPascal() / 1000f;
    public static float GigaPascalToPascal(this float GPa) => GPa.GigaPascalToKiloPascal() / 1000f;
    public static float GigaPascalToKiloPascal(this float GPa) => GPa.GigaPascalToMegaPascal() / 1000f;
    public static float GigaPascalToMegaPascal(this float GPa) => GPa / 1000f;

    public static float MicroPascalToPsi(this float uPa) => uPa.MicroPascalToKiloPascal() / 6.89476f;
    public static float MilliPascalToPsi(this float mPa) => mPa.MilliPascalToKiloPascal() / 6.89476f;
    public static float PascalToPsi(this float Pa) => Pa.PascalToKiloPascal() / 6.89476f;
    public static float KiloPascalToPsi(this float kPa) => kPa / 6.89476f;
    public static float KiloPascalToPsi(this PressurekPa kPa) => kPa.ToFloat() / 6.89476f;
    public static float MegaPascalToPsi(this float MPa) => MPa.MegaPascalToKiloPascal() / 6.89476f;
    public static float GigaPascalToPsi(this float GPa) => GPa.GigaPascalToKiloPascal() / 6.89476f;

    public static float PsiToMicroPascal(this float Psi) => Psi.PsiToMilliPascal() * 1000;
    public static float PsiToMilliPascal(this float Psi) => Psi.PsiToPascal() * 1000;
    public static float PsiToPascal(this float Psi) => Psi.PsiToKiloPascal() * 1000;
    public static float PsiToKiloPascal(this float Psi) => Psi * 6.89476f;
    public static float PsiToMegaPascal(this float Psi) => Psi.PsiToKiloPascal() / 1000;
    public static float PsiToGigaPascal(this float Psi) => Psi.PsiToMegaPascal() / 1000;

    public static float BarToMicroPascal(this float Bar) => Bar.BarToMilliPascal() * 100f;
    public static float BarToMilliPascal(this float Bar) => Bar.BarToPascal() * 100f;
    public static float BarToPascal(this float Bar) => Bar.BarToKiloPascal() * 100f;
    public static float BarToKiloPascal(this float Bar) => Bar * 100f;
    public static float BarToMegaPascal(this float Bar) => Bar / 10f;
    public static float BarToGigaPascal(this float Bar) => Bar.BarToMegaPascal() / 1000f;

    public static float MicroPascalToBar(this float uPa) => uPa.MicroPascalToKiloPascal() / 100f;
    public static float MilliPascalToBar(this float mPa) => mPa.MilliPascalToKiloPascal() / 100f;
    public static float PascalToBar(this float Pa) => Pa.PascalToKiloPascal() / 100f;
    public static float KiloPascalToBar(this float kPa) => kPa / 100f;
    public static float KiloPascalToBar(this PressurekPa kPa) => kPa.ToFloat().KiloPascalToBar();
    public static float MegaPascalToBar(this float MPa) => MPa.MegaPascalToKiloPascal() / 100f;
    public static float GigaPascalToBar(this float GPa) => GPa.GigaPascalToKiloPascal() / 100f;

    public static float LiterToImperialGallon(this float liter) => liter / 4.546f;
    public static float LiterToUSGallon(this float liter) => liter * 1.057f;
    public static float LiterToCubicInch(this float liter) => liter * 61.024f;

    public static float LiterToImperialGallon(this VolumeLitres liter) => liter.ToFloat().LiterToImperialGallon();
    public static float LiterToUSGallon(this VolumeLitres liter) => liter.ToFloat().LiterToUSGallon();
    public static float LiterToCubicInch(this VolumeLitres liter) => liter.ToFloat().LiterToCubicInch();

    public static float PartialMoles(this Atmosphere atmosphere, Chemistry.GasType gasType) => atmosphere.TotalMoles.ToFloat() * atmosphere.GetGasTypeRatio(gasType);
  }

  public static class SpeciesExtensions
  {
    public static Chemistry.GasType GetAirType(this SpeciesClass species) => species switch
    {
      SpeciesClass.Human => Chemistry.GasType.Oxygen,
      SpeciesClass.Zrilian => Chemistry.GasType.Volatiles,
      _ => Chemistry.GasType.Undefined,
    };
  }
}
