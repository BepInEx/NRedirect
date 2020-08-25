using System;
using System.Collections.Generic;

namespace NRedirect.Generator
{
	public class Arguments
	{
		public IReadOnlyDictionary<string, string> Switches { get; protected set; }

		public IList<string> RegisteredValueKeys { get; protected set; }

		public IList<string> Values { get; protected set; }

		public string this[string switchKey] =>
			Switches.ContainsKey(switchKey)
				? Switches[switchKey]
				: null;

		public string this[int valueIndex] => Values[valueIndex];

		public bool Flag(string switchKey) => Switches.ContainsKey(switchKey);

		public Arguments(Dictionary<string, string> switches, List<string> values)
		{
			Values = values;
			Switches = switches;
		}

		public static Arguments Parse(string[] args, IList<string> registeredValueKeys = null)
		{
			registeredValueKeys ??= new List<string>();

			Dictionary<string, string> switches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			List<string> values = new List<string>();

			string previousSwitch = null;
			bool valuesOnly = false;

			foreach (string arg in args)
			{
				if (arg == "--")
				{
					// no more switches, only values
					valuesOnly = true;

					continue;
				}

				if (valuesOnly)
				{
					values.Add(arg);
					continue;
				}

				if (arg.StartsWith("-")
				    || arg.StartsWith("--"))
				{
					if (previousSwitch != null)
						switches.Add(previousSwitch, string.Empty);

					if (arg.StartsWith("--"))
						previousSwitch = arg.Substring(2);
					else
						previousSwitch = arg.Substring(1);

					if (!registeredValueKeys.Contains(previousSwitch))
					{
						switches.Add(previousSwitch, string.Empty);
						previousSwitch = null;
					}

					continue;
				}

				if (previousSwitch != null)
				{
					switches.Add(previousSwitch, arg);
					previousSwitch = null;
				}
				else
				{
					values.Add(arg);
				}
			}

			if (previousSwitch != null)
				switches.Add(previousSwitch, string.Empty);

			return new Arguments(switches, values);
		}
	}
}