﻿using System;
using System.Xml;
using NUnit.Framework;
using Palaso.DictionaryServices.Processors;
using Palaso.IO;
using Palaso.Progress.LogBox;
using Palaso.TestUtilities;

namespace Palaso.DictionaryServices.Tests.Merging
{
	public class MergeHomographsTests
	{
		private TempFile _outputLift;
		private StringBuilderProgress _progress;
		private XmlDocument _resultDom;

		[SetUp]
		public void Setup()
		{
			_outputLift = new TempFile();
			_progress = new StringBuilderProgress();
			_resultDom = new XmlDocument();
		}



		[Test]
		public void Run_NoEntries_HeaderPreserved()
		{
			Run(@"<header>
					<ranges>
						<range href='file://C:/dev/temp/sena3/sena3.lift-ranges' id='dialect'></range>
						<range href='file://C:/dev/temp/sena3/sena3.lift-ranges' id='etymology'></range>
					</ranges>
				</header>");
			AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("lift/header/ranges/range", 2);

		}

		[Test, Ignore("Waiting for LIFT version which supports metada")]
		public void Run_NoEntries_MetadataInAnotherNameSpacePreserved()
		{
			Run(@"<header>
					<metadata>
						 <olac:olac xmlns:olac='http://www.language-archives.org/OLAC/1.1/'
							  xmlns:dc='http://purl.org/dc/elements/1.1/'
							  xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'
							  xsi:schemaLocation='http://www.language-archives.org/OLAC/1.1/
								 http://www.language-archives.org/OLAC/1.1/olac.xsd'
							  <dc:creatorBloomfield, Leonard</dc:creator>
							  <dc:date>1933</dc:date>
							  <dc:titleLanguage</dc:title>
							  <dc:publisherNew York: Holt</dc:publisher>
						   </olac:olac>
					</metadata>
				</header>");

			//todo:
			XmlNamespaceManager nameSpaceManager = new XmlNamespaceManager(new NameTable());
			nameSpaceManager.AddNamespace("olac", "http://www.language-archives.org/OLAC/1.1/");
			nameSpaceManager.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
			AssertThatXmlIn.Dom(_resultDom).HasAtLeastOneMatchForXpath("lift/header/metadata/olac:olac/dc:", nameSpaceManager);
		}

		[Test]
		public void Run_NoHomographs_OK()
		{
			var contents = @"
				<entry id='foo' GUID1>
					<lexical-unit>
						  <form lang='etr'><text>hello</text></form>
					</lexical-unit>
				</entry>
				<entry GUID2>
					<lexical-unit>
						  <form lang='etr'><text>bye</text></form>
					</lexical-unit>
				</entry>";
			Run(contents);
			AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 2);
		}


		[Test]
		public void Run_DiffModifiedDates_NewerModifiedDateUsed()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
				</entry>",
				() =>
					{
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
							"//entry[@dateModified='2009-10-02T01:42:57Z']", 1);
					});
		}

		[Test]
		public void Run_MultipleWritingSystemsInLexicalUnit_PicksMoreFrequentOne()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='less'><text>fooLess</text></form>
						   <form lang='more'><text>fooMore</text></form>
					</lexical-unit>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						   <form lang='more'><text>fooMore</text></form>
					</lexical-unit>
				</entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
						"//entry/lexical-unit/form", 2);
				});
		}


		[Test]
		public void Run_OneHasCitationForm_Merged()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<citation>
						<form lang='seh'><text>suzumira</text></form>
					</citation>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
				</entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
						"//entry/citation", 1);
				});
		}


		[Test]
		public void Run_BothHaveSaveOneWritingSystemGloss_Merged()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<sense>
						<gloss lang='en'><text>blah</text></gloss>
					</sense>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
				   <sense>
						<gloss lang='en'><text>blah</text></gloss>
					</sense>
				</entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
						"//entry/sense", 1);
				});
		}

		[Test]
		public void Run_SensesWithSamePartOfSpeech_Merged()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<sense>
						<gloss lang='en'><text>blah</text></gloss>
						  <grammatical-info value='Nome'></grammatical-info>
					</sense>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
				   <sense>
						<gloss lang='en'><text>blah</text></gloss>
						  <grammatical-info value='Nome'></grammatical-info>
					</sense>
				</entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
						"//entry/sense", 1);
				});
		}
		[Test]
		public void Run_MergableGlosses_MergesGlosses()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<sense>
						<gloss lang='en'><text>blah</text></gloss>
					   <gloss lang='fr'><text>bla</text></gloss>
					</sense>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
				   <sense>
						<gloss lang='en'><text>blah</text></gloss>
					<gloss lang='ipa'><text>pla</text></gloss>
					</sense>
				</entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
						"//entry/sense", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
						"//entry/sense/gloss/text", 3);
				});
		}

		//<field type='myInfo'><form lang='seh'><text>malambe</text></form></field>

		[Test]
		public void Run_OneSenseHasTraitConflicts_SenseNotMerged()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<sense>
						<gloss lang='en'><text>blah</text></gloss>
						<trait name='t1' value='1'></trait>
					</sense>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
				   <sense>
						<gloss lang='en'><text>blah</text></gloss>
						 <trait name='t1' value='2'></trait>
					</sense>
				</entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
						"//entry/sense", 2);
				});
		}

		[Test]
		public void Run_OneSenseHasTraitsWhichDontConflict_SenseMerged()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<sense>
						<gloss lang='en'><text>blah</text></gloss>
						<trait name='t1' value='1'></trait>
						<trait name='t3' value='3'></trait>
					</sense>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
				   <sense>
						<gloss lang='en'><text>blah</text></gloss>
					   <trait name='t1' value='1'></trait>
						 <trait name='t2' value='2'></trait>
					</sense>
				</entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
						"//entry/sense", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
						"//entry/sense/trait", 3);
				});
		}

		[Test]
		public void Run_BothHaveOneDefinitionWithSameWS_OnlyOneSenseRemains()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<sense>
						<definition>
							<form lang='en'><text>blah</text></form>
						</definition>
					</sense>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
				   <sense>
					   <definition>
							<form lang='en'><text>blah</text></form>
						</definition>
					</sense>
				</entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
						"//entry/sense", 1);
				});
		}



		[Test]
		public void Run_HasCompatibleTraitsAtEntryLevel_Merged()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<trait name='t1' value='1'></trait>
					<trait name='morph-type' value='root'></trait>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<trait name='t2' value='2'></trait>
					<trait name='morph-type' value='root'></trait>
				</entry>",
				() =>
					{
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
							"//entry/trait[@name='t1']", 1);
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
							"//entry/trait[@name='t2']", 1);
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
							"//entry/trait[@name='morph-type']", 1);
					});
		}

		[Test]
		public void Run_HasIncompatibleFieldsAtEntryLevel_NotMerged()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>

					<field type='Plural'><form lang='seh'><text>xxxx</text></form></field>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					 <field type='Plural'><form lang='seh'><text>yyy</text></form></field>
				</entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 2);
				});
		}

		[Test]
		public void Run_HasIncompatibleCitationForms_NotMerged()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<citation>
						<form lang='seh'><text>xxx</text></form>
					</citation>
				</entry>",
				@"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<citation>
						<form lang='seh'><text>yyy</text></form>
					</citation>
					<field type='Plural'><form lang='seh'><text>yyy</text></form></field>
				</entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 2);
				});
		}
		[Test]
		public void Run_ComplexEntryFromSena_NothingLost()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
						<lexical-unit>
						  <form lang='etr'>
							<text>bandazi</text>
						  </form>
						</lexical-unit>
				</entry>",
				@"<entry dateCreated='2005-06-23T01:30:30Z' dateModified='2006-09-06T01:12:06Z' GUID2>
						<lexical-unit>
						  <form lang='etr'>
							<text>bandazi</text>
						  </form>
						</lexical-unit>
						<trait name='morph-type' value='root'></trait>
						<field type='Plural'>
						  <form lang='seh'>
							<text>abandazi</text>
						  </form>
						</field>
						<relation type='_component-lexeme' ref='bodzi_d333f64f-d388-431f-bb2b-7dd9b7f3fe3c'>
							<trait name='complex-form-type' value='Composto'></trait>
							<trait name='is-primary' value='true'/>
						</relation>
					  </entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry/relation", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry/relation/trait[@name='complex-form-type']",1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry/relation/trait[@name='is-primary']", 1);
				});
		}

		[Test]
		public void Run_ComplexSenseFromSena_NothingLost()
		{
			MergeTwoAndTest(
				@"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
						<lexical-unit>
						  <form lang='etr'>
							<text>bandazi</text>
						  </form>
						</lexical-unit>
						<sense>
							<gloss lang='en'>
							<text>servant</text>
						  </gloss>
						</sense>
				</entry>",
				@"<entry dateCreated='2005-06-23T01:30:30Z' dateModified='2006-09-06T01:12:06Z' GUID2>
						<lexical-unit>
						  <form lang='etr'>
							<text>bandazi</text>
						  </form>
						</lexical-unit>
						<sense>
						  <grammatical-info value='Nome'></grammatical-info>
						  <gloss lang='en'>
							<text>servant</text>
						  </gloss>
						  <gloss lang='pt'>
							<text>empregado</text>
						  </gloss>
						  <definition>
							<form lang='en'>
							  <text>servant</text>
							</form>
							<form lang='pt'>
							  <text>empregado de casa</text>
							</form>
						  </definition>
						  <example source='Moreira:14,26 (mbandazi - sic)'>
							<note type='reference'>
							  <form lang='en'>
								<text>Moreira:14,26 (mbandazi - sic)</text>
							  </form>
							</note>
						  </example>
						  <trait name='usage-type' value='archaic'></trait>
						</sense>
					  </entry>",
				() =>
				{
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry/sense", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry/sense/trait[@name='usage-type']", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry/sense/example", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry/sense/definition", 1);
					AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry/sense/grammatical-info", 1);
				});
		}
//        [Test]
//        public void Run_ClashingMorphType_DoesNotMerge()
//        {
//            MergeTwoAndTest(
//                @"<entry id='foo' GUID1 dateModified='2006-10-02T01:42:57Z'>
//                    <lexical-unit>
//                          <form lang='en'><text>foo</text></form>
//                    </lexical-unit>
//                     <trait name='morph-type' value='root'></trait>
//                </entry>",
//                @"<entry GUID2 dateModified='2009-10-02T01:42:57Z'>
//                    <lexical-unit>
//                          <form lang='en'><text>foo</text></form>
//                    </lexical-unit>
//                    <trait name='morph-type' value='enclitic'></trait>
//                </entry>",
//                () =>
//                {
//                    AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//entry", 2);
//                });
		//}

		[Test]
		public void Run_ComplicatedSense_FullSensesPreserved()
		{
			MergeTwoAndTest(
				@"
				<entry id='foo1' guid='57009cdb-cd11-451f-8340-05dce62cc000'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
					<sense id='senseId1' order='4'>
						<grammatical-info value='Associativo'>
							<trait name='type' value='inflAffix'></trait>
							<trait name='Preposição-slot' value='assocncl'></trait>
						</grammatical-info>
						<gloss lang='en'><text>english1</text></gloss>
						<gloss lang='pt'><text>portugues1</text></gloss>
					</sense>
				</entry>",
				@"<entry id='blah' guid='57009cdb-cd11-451f-8340-05dce62cc001'>
					<lexical-unit>
						  <form lang='etr'><text>blah</text></form>
					</lexical-unit>
				</entry>
				<entry id='foo2' guid='57009cdb-cd11-451f-8340-05dce62cc002'>
					<lexical-unit>
						  <form lang='etr'><text>foo</text></form>
					</lexical-unit>
				   <sense id='senseId2' order='4'>
						<grammatical-info value='Nombre'>
							<trait name='type' value='inflAffix'></trait>
							<trait name='Preposição-slot' value='assocncl'></trait>
						</grammatical-info>
						<gloss lang='en'><text>english2</text></gloss>
						<gloss lang='pt'><text>portugues2</text></gloss>
					</sense>
				</entry>",
				() =>
					{
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath("//lexical-unit", 2);
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
							"//lexical-unit/form/text[text()='foo']", 1);
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
							"//entry[lexical-unit/form/text[text()='foo']]/sense", 2);
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
							"//entry[lexical-unit/form/text[text()='foo']]/sense[@id='senseId2']/grammatical-info/trait",
							2);
						AssertThatXmlIn.Dom(_resultDom).HasSpecifiedNumberOfMatchesForXpath(
							"//entry[lexical-unit/form/text[text()='foo']]/sense[@id='senseId2']/gloss", 2);
					});
		}

		private void Run(string contents)
		{
			var m = new HomographMerger();
			contents = contents.Replace("GUID1", "guid='"+Guid.NewGuid().ToString()+"'");
			contents = contents.Replace("GUID2", "guid='" + Guid.NewGuid().ToString() + "'");
			using (var input = new TempLiftFile(contents, "0.13"))
			{
				using (var repo = new LiftLexEntryRepository(input.Path))
				{
					var ws = HomographMerger.GuessPrimarLexicalFormWritingSystem(repo, _progress);
					HomographMerger.Merge(repo, ws, _progress);
				}

				_resultDom.Load(input.Path);

				//removing these tombstones simplifies our assertions, later
				foreach (XmlNode deletedEntry in _resultDom.SelectNodes("//entry[@dateDeleted]"))
				{
					deletedEntry.ParentNode.RemoveChild(deletedEntry);
				}
			}
		}

		private void MergeTwoAndTest(string entry1, string entry2, Action tests)
		{
			//run in both orders, so as to reduce the chance of missing some important distinction based on which is merged into which
			Run(entry1 + entry2);
			tests();
			Run(entry2 + entry1);
			tests();
		}
	}
}
