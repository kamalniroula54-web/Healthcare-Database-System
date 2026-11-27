using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using PhoneNumbers;

namespace MK_Hospital.Data;

public sealed record PhoneCountry(string RegionCode, int CallingCode, string Name);

public sealed record PhoneNumberInput(string RegionCode, string NationalNumber);

public sealed record PhoneFormatRequest(string? Value, string? RegionCode);

public static class PhoneNumberSupport
{
	public const string DefaultRegion = "US";

	private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();
	private static readonly HashSet<string> SupportedRegions =
		Util.GetSupportedRegions().ToHashSet(StringComparer.OrdinalIgnoreCase);

	public static IReadOnlyList<PhoneCountry> Countries { get; } = CreateCountries();

	public static IEnumerable<SelectListItem> CountryOptions =>
		Countries.Select(country => new SelectListItem(
			$"{country.Name} (+{country.CallingCode})",
			country.RegionCode));

	public static bool TryNormalize(string? value, string? regionCode, out string normalized)
	{
		normalized = string.Empty;
		if (string.IsNullOrWhiteSpace(value) ||
			string.IsNullOrWhiteSpace(regionCode) ||
			!SupportedRegions.Contains(regionCode))
		{
			return false;
		}

		try
		{
			PhoneNumber number = Util.Parse(value, regionCode);
			if (!Util.IsValidNumber(number))
				return false;

			normalized = Util.Format(number, PhoneNumberFormat.E164);
			return true;
		}
		catch (NumberParseException)
		{
			return false;
		}
	}

	public static bool TryFormatAsYouType(string? value, string? regionCode, out string formatted)
	{
		formatted = string.Empty;
		if (string.IsNullOrWhiteSpace(regionCode) || !SupportedRegions.Contains(regionCode))
			return false;

		AsYouTypeFormatter formatter = Util.GetAsYouTypeFormatter(regionCode);
		foreach (char character in value ?? string.Empty)
		{
			if (character == '+' && formatted.Length == 0)
				formatted = formatter.InputDigit(character);
			else if (character >= '0' && character <= '9')
				formatted = formatter.InputDigit(character);
		}

		return true;
	}

	public static string NormalizeOrThrow(string value, string regionCode)
	{
		if (!TryNormalize(value, regionCode, out string normalized))
			throw new FormatException("The phone number is invalid for the selected country.");

		return normalized;
	}

	public static PhoneNumberInput ToInput(string? value)
	{
		string rawValue = value?.Trim() ?? string.Empty;
		if (!string.IsNullOrEmpty(rawValue))
		{
			try
			{
				string? parseRegion = rawValue.StartsWith('+') ? null : DefaultRegion;
				PhoneNumber number = Util.Parse(rawValue, parseRegion);
				if (Util.IsValidNumber(number))
				{
					string regionCode = Util.GetRegionCodeForNumber(number);
					if (!SupportedRegions.Contains(regionCode))
						regionCode = DefaultRegion;

					return new PhoneNumberInput(
						regionCode,
						Util.Format(number, PhoneNumberFormat.NATIONAL));
				}
			}
			catch (NumberParseException)
			{
			}
		}

		return new PhoneNumberInput(DefaultRegion, rawValue);
	}

	public static string FormatInternational(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		try
		{
			string? parseRegion = value.TrimStart().StartsWith('+') ? null : DefaultRegion;
			PhoneNumber number = Util.Parse(value, parseRegion);
			return Util.IsValidNumber(number)
				? Util.Format(number, PhoneNumberFormat.INTERNATIONAL)
				: value;
		}
		catch (NumberParseException)
		{
			return value;
		}
	}

	private static IReadOnlyList<PhoneCountry> CreateCountries()
	{
		List<PhoneCountry> countries = [];
		foreach (string regionCode in SupportedRegions.OrderBy(code => code, StringComparer.Ordinal))
		{
			if (regionCode.Length != 2)
				continue;

			try
			{
				int callingCode = Util.GetCountryCodeForRegion(regionCode);
				if (callingCode > 0)
				{
					countries.Add(new PhoneCountry(
						regionCode,
						callingCode,
						new RegionInfo(regionCode).EnglishName));
				}
			}
			catch (ArgumentException)
			{
			}
		}

		return countries
			.OrderBy(country => country.Name, StringComparer.CurrentCulture)
			.ToArray();
	}
}
