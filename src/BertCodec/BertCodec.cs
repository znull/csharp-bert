using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Erlectric;

// http://bert-rpc.org/

namespace BertCodec {
	public class BertCodec {
		internal static readonly Atom bert  = new Atom("bert");
		internal static readonly Atom dict  = new Atom("dict");
		internal static readonly Atom nil   = new Atom("nil");
		internal static readonly Atom regex = new Atom("regex");
		internal static readonly Atom time  = new Atom("time");
		internal static readonly Atom True  = new Atom("true");
		internal static readonly Atom False = new Atom("false");

		static readonly ETFTuple BertNil = new ETFTuple() { bert, nil };
		static readonly ETFTuple BertTrue = new ETFTuple() { bert, True };
		static readonly ETFTuple BertFalse = new ETFTuple() { bert, False };

		internal static readonly Atom caseless = new Atom("caseless");
		internal static readonly Atom unicode = new Atom("unicode");
		internal static readonly Atom multiline = new Atom("multiline");
		internal static readonly Atom no_auto_capture = new Atom("no_auto_capture");
		internal static readonly Atom dotall = new Atom("dotall");

		static Encoding utf8 = Encoding.GetEncoding("UTF-8",
				new EncoderExceptionFallback(),
				new DecoderExceptionFallback());

		static readonly Dictionary<Atom, RegexOptions> RegexOptionTranslations = new Dictionary<Atom, RegexOptions>() {
			{ caseless,		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant },
			{ dotall,		RegexOptions.Singleline },
			{ multiline,		RegexOptions.Multiline },
			{ no_auto_capture,	RegexOptions.ExplicitCapture },
			{ unicode,		RegexOptions.None },
		};

		static readonly DateTime BeginEpoch = new DateTime(1970, 1, 1);
		static readonly long TicksPerMegaSecond = TimeSpan.TicksPerSecond * 1000000;

		public static byte[] Encode(object obj) {
			return ETFCodec.Encode(bertEncode(obj));;
		}

		static object bertEncode(object obj) {
			if(obj == null) {
				obj = BertNil;
			} else if(obj is bool) {
				obj = (bool)obj ? BertTrue : BertFalse;
			} else if(obj is ETFTuple) {	// has to come before IList case so tuples won't be converted
				var tuple = (ETFTuple)obj;
				var nt = new ETFTuple(tuple.Count);
				foreach(var e in tuple) {
					nt.Add(bertEncode(e));
				}
				obj = nt;
			} else if(obj is byte[]) {
				// don't treat byte[] as IList
			} else if(obj is IList) {
				var il = (IList)obj;
				var nl = new ArrayList(il.Count);
				foreach(var e in il) {
					nl.Add(bertEncode(e));
				}
				obj = nl;
			} else if(obj is IDictionary) {
				var id = (IDictionary)obj;
				var items = new ArrayList(id.Count);
				foreach(DictionaryEntry e in id) {
					items.Add(new ETFTuple() { bertEncode(e.Key), bertEncode(e.Value) });
				}
				obj = new ETFTuple() { bert, dict, items };
			} else if(obj is IDictionary<string, object>) {
				var id = (IDictionary<string, object> )obj;
				var items = new ArrayList(id.Count);
				foreach(var e in id) {
					items.Add(new ETFTuple() { bertEncode(e.Key), bertEncode(e.Value) });
				}
				obj = new ETFTuple() { bert, dict, items };
			} else if(obj is DateTime) {
				var dt = (DateTime)obj;
				TimeSpan span = dt - BeginEpoch;
				long megaSeconds = span.Ticks / TicksPerMegaSecond;
				long seconds = (span.Ticks % TicksPerMegaSecond) / TimeSpan.TicksPerSecond;
				long milliSeconds = (span.Ticks % TimeSpan.TicksPerSecond) / TimeSpan.TicksPerMillisecond;
				obj = new ETFTuple() { bert, time, megaSeconds, seconds, milliSeconds };
			} else if(obj is Regex) {
				obj = encodeRegex((Regex)obj);
			}
			return obj;
		}

		static ETFTuple encodeRegex(Regex rx) {
			var options = new ArrayList() { unicode };
			var rxopts = rx.Options;
			if(rxopts.HasFlag(RegexOptions.IgnoreCase)) {
				if((!rx.Options.HasFlag(RegexOptions.CultureInvariant))) {
					throw new NotSupportedException("IgnoreCase regex must be CultureInvariant");
				}

				options.Add(caseless);
				rxopts &= ~(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			}
			if(rxopts.HasFlag(RegexOptions.Multiline)) {
				options.Add(multiline);
				rxopts &= ~RegexOptions.Multiline;
			}
			if(rxopts.HasFlag(RegexOptions.ExplicitCapture)) {
				options.Add(no_auto_capture);
				rxopts &= ~RegexOptions.ExplicitCapture;
			}
			if(rxopts.HasFlag(RegexOptions.Singleline)) {
				options.Add(dotall);
				rxopts &= ~RegexOptions.Singleline;
			}
			if(rxopts != RegexOptions.None) {
				throw new NotSupportedException(string.Format("regex options: {0}", rxopts));
			}
			var source = Encoding.UTF8.GetBytes(rx.ToString());
			return new ETFTuple() { bert, regex, source, options };
		}

		public static object Decode(byte[] bytes) {
			return bertDecode(ETFCodec.Decode(bytes));
		}

		internal static object bertDecode(object obj) {
			if(obj is ETFTuple) {
				var tuple = (ETFTuple)obj;
				if(tuple.Count > 1 && bert.Equals(tuple[0])) {
					switch(tuple.Count) {
						case 2:
							if(nil.Equals(tuple[1])) {
								obj = null;
							} else if(True.Equals(tuple[1])) {
								obj = true;
							} else if(False.Equals(tuple[1])) {
								obj = false;
							} else {
								goto default;
							}
							break;

						case 3:
							if(dict.Equals(tuple[1])) {
								var il = (IList)tuple[2];
								var ht = new Hashtable(il.Count);
								foreach(object o in il) {
									ETFTuple item = (ETFTuple)o;
									var key = bertDecode(item[0]);
									byte[] b = key as byte[];
									if(b != null) {
										lock(utf8) {
											key = utf8.GetString(b, 0, b.Length);
										}
									}
									ht.Add(key, bertDecode(item[1]));
								}
								obj = ht;
								break;
							}
							goto default;

						case 4:
							if(regex.Equals(tuple[1])) {
								var options = RegexOptions.None;
								foreach(var opt in ((IList)tuple[3]).Cast<Atom>()) {
									RegexOptions optflag;
									if(RegexOptionTranslations.TryGetValue(opt, out optflag)) {
										options |= optflag;
									} else {
										throw new NotSupportedException(string.Format("regex option: {0}", opt));
									}
								}

								string pattern;
								lock(utf8) {
									pattern = utf8.GetString((byte[])tuple[2]);
								}
								obj = new Regex(pattern, options);
								break;
							}
							goto default;

						case 5:
							if(time.Equals(tuple[1])) {
								obj = BeginEpoch.Add(new TimeSpan(
									  Convert.ToInt64(tuple[2]) * TicksPerMegaSecond
									+ Convert.ToInt64(tuple[3]) * TimeSpan.TicksPerSecond
									+ Convert.ToInt64(tuple[4]) * TimeSpan.TicksPerMillisecond
								));
								break;
							}
							goto default;

						default:
							throw new NotSupportedException(string.Format("invalid bert type: {0}", tuple[1]));
					}
				} else {
					var nt = new ETFTuple(tuple.Count);
					foreach(var e in tuple) {
						nt.Add(bertDecode(e));
					}
					obj = nt;
				}
			} else if(obj is byte[]) {
				// don't treat byte[] as IList
			} else if(obj is IList) {
				var il = (IList)obj;
				var nl = new ArrayList(il.Count);
				foreach(var e in il) {
					nl.Add(bertDecode(e));
				}
				obj = nl;
			}

			return obj;
		}
	}
}
