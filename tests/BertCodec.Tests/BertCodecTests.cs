using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.RegularExpressions;
using System.Linq;

namespace BertCodec.Tests {
	using NUnit.Framework;
	using Erlectric;

	[TestFixture]
	public class BertTests {
		[Test]
		public void testNumeric() {
			TestRoundtrip((byte)7);
			TestRoundtrip((byte)0);
			TestRoundtrip((byte)255);
			TestRoundtrip((int)-1);
			TestRoundtrip(Int32.MinValue);
			TestRoundtrip(Int32.MaxValue);
			TestRoundtrip(Int64.MinValue);
			TestRoundtrip(Int64.MaxValue, typeof(UInt64));

			TestRoundtrip(UInt32.MinValue, typeof(Byte));
			TestRoundtrip(UInt32.MaxValue, typeof(UInt64));
			TestRoundtrip(UInt64.MinValue, typeof(Byte));
			TestRoundtrip(UInt64.MaxValue);

			TestRoundtrip(Double.MinValue);
			TestRoundtrip(Double.MaxValue);
			TestRoundtrip(Double.Epsilon);
			TestRoundtrip(Double.NaN);
			TestRoundtrip(Double.NegativeInfinity);
			TestRoundtrip(Double.PositiveInfinity);
		}

		[Test, Sequential]
		public void TestString([Values("", "nonempty", "unicode: ᚠᛇᚻ᛫ᛒᛦᚦ᛫ᚠᚱᚩᚠᚢᚱ᛫ᚠᛁᚱᚪ᛫ᚷᛖᚻᚹᛦᛚᚳᚢᛗ")] string s) {
			Assert.That(Roundtrip(s), Is.EqualTo(ETFCodec.ToBytes(s)));
		}

		[Test]
		public void testAtom() {
			TestRoundtrip(new Atom(""));
			TestRoundtrip(new Atom("nonempty"));
		}

		[Test]
		public void testTuple() {
			Assert.That(new byte[] { Constants.FORMAT_VERSION, Constants.SMALL_TUPLE_EXT, 0 },
					Is.EqualTo(BertCodec.Encode(new ETFTuple())));
			TestRoundtrip(new ETFTuple());
			TestRoundtrip(new ETFTuple() { null });
			TestRoundtrip(new ETFTuple() { new ArrayList(), new ETFTuple(), new ETFTuple() { null }, 17 });
			TestRoundtrip(new ETFTuple(Enumerable.Range(0, 300).ToList()));
		}

		[Test]
		public void testList() {
			TestRoundtrip(new ArrayList());
			TestRoundtrip(new ArrayList() { 17, null, new ArrayList() { 0.0, null }, 7.7 });
		}

		[Test]
		public void testNil() {
			TestRoundtrip(null);
		}

		[Test]
		public void testBool() {
			TestRoundtrip(true);
			TestRoundtrip(false);
		}

		[Test]
		public void testBinary() {
			var bytes = new byte[] { 7, 8, 9, 0, 255 };
			TestRoundtrip(new byte[0]);
			TestRoundtrip(new byte[] { 7, 8, 9, 0, 255 });
			TestRoundtrip(new ETFTuple() { bytes });
			TestRoundtrip(new ArrayList() { null, ETFCodec.ToBytes("foo"), new byte[] { 9, 0, 255 } });
		}

		[Test]
		public void testTime() {
			var times = new DateTime[] {
				DateTime.Now,
				DateTime.MinValue,
				DateTime.MaxValue,
			};
			foreach(var dt in times) {
				var recode = (DateTime)BertCodec.Decode(BertCodec.Encode(dt));
				Assert.That(Math.Abs((dt - recode).Ticks), Is.LessThan(TimeSpan.TicksPerMillisecond));
			}
		}

		[Test]
		public void testRegex() {
			var rxlist = new Regex[] {
				new Regex(""),
				new Regex("foo", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant),
				new Regex("x.*y", RegexOptions.Multiline),
				new Regex("a", RegexOptions.ExplicitCapture),
				new Regex("b", RegexOptions.Singleline),
			};
			foreach(var rx in rxlist) {
				var recode = (Regex)BertCodec.Decode(BertCodec.Encode(rx));
				AssertRegexEqual(recode, rx);
			}

			/* It would be nice to translate
			 * IgnorePatternWhitespace to erlang's "extended", but
			 * they differ in their handling of whitespace within
			 * character classes. Maybe that's ok, given other
			 * possible differences of interpretation? */
			Assert.Throws<NotSupportedException>( () => { BertCodec.Encode(new Regex("", RegexOptions.IgnorePatternWhitespace)); } );
			Assert.Throws<NotSupportedException>( () => { BertCodec.Encode(new Regex("", RegexOptions.RightToLeft)); } );
			Assert.Throws<NotSupportedException>( () => { BertCodec.Encode(new Regex("", RegexOptions.ECMAScript)); } );
			Assert.Throws<NotSupportedException>( () => { BertCodec.Encode(new Regex("", RegexOptions.IgnoreCase)); } );
			Assert.Throws<NotSupportedException>( () => { BertCodec.Encode(new Regex("", RegexOptions.CultureInvariant)); } );

			var decodeExamples = new List<Tuple<byte[], ArrayList, Regex>>() {
				Tuple.Create(new byte[0], new ArrayList(), new Regex("")),
				Tuple.Create(new byte[0], new ArrayList() { BertCodec.caseless },
						new Regex("", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)),
				Tuple.Create(new byte[0], new ArrayList() { BertCodec.caseless, BertCodec.dotall },
						new Regex("", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.Singleline)),
				Tuple.Create(new byte[0], new ArrayList() { BertCodec.multiline },
						new Regex("", RegexOptions.Multiline)),
				Tuple.Create(new byte[0], new ArrayList() { BertCodec.no_auto_capture },
						new Regex("", RegexOptions.ExplicitCapture)),
				new Tuple<byte[], ArrayList, Regex>(new byte[0], new ArrayList() { new Atom("invalid") }, null),
			};
			foreach(var t in decodeExamples) {
				var rx = new ETFTuple() { BertCodec.bert, BertCodec.regex, t.Item1, t.Item2 };
				if(t.Item3 == null) {
					Assert.Throws<NotSupportedException>( () => BertCodec.bertDecode(rx));
				} else {
					AssertRegexEqual(t.Item3, (Regex)BertCodec.bertDecode(rx));
				}
			}
		}

		void AssertRegexEqual(Regex expect, Regex actual) {
			Assert.That(actual.ToString(), Is.EqualTo(expect.ToString()));
			Assert.That(actual.Options, Is.EqualTo(expect.Options));
		}

		[Test]
		public void testDict() {
			TestRoundtrip(new Hashtable());
			var ht = new Hashtable() {
				{ 0.1, 7.7 },
				{ 8.7, 9 },
				// { 2, 3 },	TODO: integral keys don't work well across bert because (int)7 becomes (byte)7
				{ -2, 3 },
				{ 666, 3 },
				{ "foo", ETFCodec.ToBytes("bar") },
				{ "", ETFCodec.ToBytes("empty") },
				{ "x", null },
				{ "a", new ETFTuple() },
				{ "b", new ETFTuple() { 7 } },
				{ "c", new Hashtable() },
				{ "d", new Hashtable() {{ "P", ETFCodec.ToBytes("Q") }} },
			};
			var encoded = BertCodec.Encode(ht);
			var reencoded = (Hashtable)BertCodec.Decode(encoded);
			Assert.That(reencoded.Keys, Is.EquivalentTo(ht.Keys));

			foreach(DictionaryEntry e in ht) {
				Assert.That(reencoded[e.Key], Is.EqualTo(e.Value));
			}
		}

		[Test]
		public void testExpando() {
			dynamic o = new ExpandoObject();
			o.foo = 777;
			o.bar = 8.88;
			o.nothing = null;
			o.tuple = new Atom("something");
			o.date = new DateTime();

			var encoded = BertCodec.Encode(o);
			var reencoded = (Hashtable)BertCodec.Decode(encoded);
			var reencDict = reencoded.Cast<DictionaryEntry>().ToDictionary(
					de => (string)de.Key, de => de.Value);
			Assert.That(reencDict, Is.EquivalentTo((IDictionary<string, object>)o));
		}

		object Roundtrip(object obj) {
			return BertCodec.Decode(BertCodec.Encode(obj));
		}

		public void TestRoundtrip(object obj) {
			TestRoundtrip(obj, obj == null ? null : obj.GetType());
		}

		public void TestRoundtrip(object obj, Type expectType) {
			var encoded1 = BertCodec.Encode(obj);
			var encoded2 = BertCodec.Encode(obj);
			var reencoded = BertCodec.Decode(encoded1);
			Assert.That(reencoded, Is.EqualTo(obj));

			// verify that decoding doesn't alter the encoded buffer
			Assert.That(encoded1, Is.EqualTo(encoded2));

			// verify that types remain what we expect
			if(expectType != null) {
				Assert.That(reencoded, Is.InstanceOf(expectType));
			}
		}
	}
}
