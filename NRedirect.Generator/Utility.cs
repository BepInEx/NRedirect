using System;
using System.Collections.Generic;
using System.Text;

namespace NRedirect.Generator
{
	public static class Utility
	{
		public static bool TryFind<T1, T2>(this IDictionary<T1, T2> dictionary,
			out KeyValuePair<T1, T2> pair,
			Predicate<KeyValuePair<T1, T2>> predicate)
		{
			foreach (var kv in dictionary)
			{
				if (predicate(kv))
				{
					pair = kv;
					return true;
				}
			}

			pair = default;
			return false;
		}

		public static string ByteArrayToHexString(byte[] Bytes)
		{
			StringBuilder result = new StringBuilder(Bytes.Length * 2);
			const string hexChars = "0123456789abcdef";

			foreach (byte B in Bytes)
			{
				result.Append(hexChars[B >> 4]);
				result.Append(hexChars[B & 0xF]);
			}

			return result.ToString();
		}
	}
}