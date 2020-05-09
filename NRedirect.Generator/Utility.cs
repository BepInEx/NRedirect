using System;
using System.Collections.Generic;

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
	}
}