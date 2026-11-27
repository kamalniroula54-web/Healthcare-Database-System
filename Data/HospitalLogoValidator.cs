using System.Buffers.Binary;

namespace MK_Hospital.Data;

public static class HospitalLogoValidator
{
	public const int MaxLogoBytes = 2 * 1024 * 1024;
	private const int MaxLogoDimension = 4096;
	private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

	public static bool TryGetContentType(byte[] content, out string? contentType)
	{
		contentType = null;
		ReadOnlySpan<byte> bytes = content;
		if (bytes.Length <= MaxLogoBytes &&
			bytes.Length >= 24 &&
			bytes[..PngSignature.Length].SequenceEqual(PngSignature) &&
			bytes[12] == (byte)'I' &&
			bytes[13] == (byte)'H' &&
			bytes[14] == (byte)'D' &&
			bytes[15] == (byte)'R' &&
			HasPngEnd(bytes))
		{
			uint width = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4));
			uint height = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4));
			if (IsValidDimension(width) && IsValidDimension(height))
			{
				contentType = "image/png";
				return true;
			}
		}

		if (bytes.Length <= MaxLogoBytes &&
			TryGetJpegDimensions(bytes, out int jpegWidth, out int jpegHeight) &&
			IsValidDimension((uint)jpegWidth) &&
			IsValidDimension((uint)jpegHeight))
		{
			contentType = "image/jpeg";
			return true;
		}

		return false;
	}

	private static bool TryGetJpegDimensions(ReadOnlySpan<byte> bytes, out int width, out int height)
	{
		width = 0;
		height = 0;
		if (bytes.Length < 4 ||
			bytes[0] != 0xff ||
			bytes[1] != 0xd8 ||
			bytes[^2] != 0xff ||
			bytes[^1] != 0xd9)
			return false;

		int offset = 2;
		while (offset < bytes.Length)
		{
			if (bytes[offset++] != 0xff)
				return false;
			while (offset < bytes.Length && bytes[offset] == 0xff)
				offset++;
			if (offset >= bytes.Length)
				return false;

			byte marker = bytes[offset++];
			if (marker is 0xd9 or 0xda)
				return false;
			if (marker == 0x01 || marker is >= 0xd0 and <= 0xd8)
				continue;
			if (offset + 2 > bytes.Length)
				return false;

			int segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
			if (segmentLength < 2 || offset + segmentLength > bytes.Length)
				return false;

			if (IsJpegStartOfFrame(marker))
			{
				if (segmentLength < 7)
					return false;
				height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2));
				width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2));
				return width > 0 && height > 0;
			}

			offset += segmentLength;
		}

		return false;
	}

	private static bool HasPngEnd(ReadOnlySpan<byte> bytes)
	{
		int offset = 8;
		while (offset + 12 <= bytes.Length)
		{
			uint chunkLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
			if (offset == 8 && chunkLength != 13)
				return false;

			long chunkEnd = offset + 12L + chunkLength;
			if (chunkEnd > bytes.Length)
				return false;

			if (bytes[offset + 4] == (byte)'I' &&
				bytes[offset + 5] == (byte)'E' &&
				bytes[offset + 6] == (byte)'N' &&
				bytes[offset + 7] == (byte)'D')
			{
				return chunkLength == 0 && chunkEnd == bytes.Length;
			}

			offset = (int)chunkEnd;
		}

		return false;
	}

	private static bool IsJpegStartOfFrame(byte marker) =>
		marker is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7 or >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf;

	private static bool IsValidDimension(uint dimension) =>
		dimension is > 0 and <= MaxLogoDimension;
}
