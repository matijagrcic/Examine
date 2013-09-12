﻿using System;
using System.Linq;
using Examine.LuceneEngine.Providers;
using Examine.LuceneEngine.SearchCriteria;
using Examine.SearchCriteria;

using Lucene.Net.Store;
using NUnit.Framework;
using UmbracoExamine;


namespace Examine.Test.Search
{
    [TestFixture]
	public class FluentApiTests
    {
        //[Test]
        //public void FluentApiTests_Grouped_Or_Examiness()
        //{
        //    ////Arrange
        //    var criteria = _searcher.CreateSearchCriteria("content");

        //    //get all node type aliases starting with CWS_Home OR and all nodees starting with "About"
        //    var filter = criteria.GroupedOr(
        //        new[] { "nodeTypeAlias", "nodeName" },
        //        new[] { "CWS\\_Home".Boost(10), "About".MultipleCharacterWildcard() })
        //        .Compile();


        //    ////Act
        //    var results = _searcher.Search(filter);

        //    ////Assert
        //    Assert.IsTrue(results.TotalItemCount > 0);
        //}


        [Test]
        public void FluentApi_Exact_Match_By_Escaped_Path()
        {
            //paths contain punctuation, we'll escape it and ensure an exact match
            var criteria = _searcher.CreateSearchCriteria("content");
            var filter = criteria.Field(UmbracoContentIndexer.IndexPathFieldName, "-1,1139,1143,1148");
            var results = _searcher.Search(filter.Compile());
            Assert.AreEqual(0, results.TotalItemCount);

            //now escape it
            var exactcriteria = _searcher.CreateSearchCriteria("content");
            var exactfilter = exactcriteria.Field(UmbracoContentIndexer.IndexPathFieldName, "-1,1139,1143,1148".Escape());
            results = _searcher.Search(exactfilter.Compile());
            Assert.AreEqual(1, results.TotalItemCount);
        }

        [Test]
		public void FluentApi_Find_By_ParentId()
		{
			var criteria = _searcher.CreateSearchCriteria("content");
			var filter = criteria.ParentId(1139);

			var results = _searcher.Search(filter.Compile());

			Assert.AreEqual(2, results.TotalItemCount);
		}

		[Test]
		public void FluentApi_Find_By_NodeTypeAlias()
		{
			var criteria = _searcher.CreateSearchCriteria("content");
			var filter = criteria.NodeTypeAlias("CWS_Home").Compile();

			var results = _searcher.Search(filter);

			Assert.IsTrue(results.TotalItemCount > 0);
		}

        [Test]
        public void FluentApi_Search_With_Stop_Words()
        {
            var criteria = _searcher.CreateSearchCriteria();
            var filter = criteria.Field("nodeName", "into")
                .Or().Field("nodeTypeAlias", "into");

            var results = _searcher.Search(filter.Compile());

            Assert.AreEqual(0, results.TotalItemCount);
        }

        [Test]
        public void FluentApi_Search_Raw_Query()
        {
            //var criteria = _searcher.CreateSearchCriteria(IndexTypes.Content);
			var criteria = _searcher.CreateSearchCriteria("content");
            var filter = criteria.RawQuery("nodeTypeAlias:CWS_Home");

            var results = _searcher.Search(filter);

            Assert.IsTrue(results.TotalItemCount > 0);
        }

        
        [Test]
        public void FluentApi_Find_Only_Image_Media()
        {

            var criteria = _searcher.CreateSearchCriteria("media");
            var filter = criteria.NodeTypeAlias("image").Compile();

            var results = _searcher.Search(filter);

            Assert.IsTrue(results.TotalItemCount > 0);

        }

        [Test]
        public void FluentApi_Find_Both_Media_And_Content()
        {          
            var criteria = _searcher.CreateSearchCriteria(BooleanOperation.Or);
            var filter = criteria
                .Field(LuceneIndexer.IndexTypeFieldName, "media")
                .Or()
				.Field(LuceneIndexer.IndexTypeFieldName, "content")
                .Compile();

            var results = _searcher.Search(filter);

            Assert.AreEqual(10, results.Count());

        }

        [Test]
        public void FluentApi_Sort_Result_By_Number_Field()
        {
            var sc = _searcher.CreateSearchCriteria("content");
            var sc1 = sc.ParentId(1143).And().OrderBy(new SortableField("sortOrder", SortType.Int)).Compile();

            var results1 = _searcher.Search(sc1).ToArray();

            var currSort = 0;
            for (var i = 0; i < results1.Count(); i++)
            {
                Assert.GreaterOrEqual(int.Parse(results1[i].Fields["sortOrder"]), currSort);
                currSort = int.Parse(results1[i].Fields["sortOrder"]);
            }
        }

        [Test]
        public void FluentApi_Sort_Result_By_Date_Field()
        {
            var sc = _searcher.CreateSearchCriteria("content");
            var sc1 = sc.ParentId(1143).And().OrderBy(new SortableField("updateDate", SortType.Double)).Compile();

            var results1 = _searcher.Search(sc1).ToArray();

            double currSort = 0;
            for (var i = 0; i < results1.Count(); i++)
            {
                Assert.GreaterOrEqual(double.Parse(results1[i].Fields["updateDate"]), currSort);
                currSort = double.Parse(results1[i].Fields["updateDate"]);
            }
        }

        [Test]
        public void FluentApi_Sort_Result_By_Single_Field()
        {
            var sc = _searcher.CreateSearchCriteria("content");
            var sc1 = sc.Field("writerName", "administrator").And().OrderBy("nodeName").Compile();

            sc = _searcher.CreateSearchCriteria("content");
            var sc2 = sc.Field("writerName", "administrator").And().OrderByDescending("nodeName").Compile();

            var results1 = _searcher.Search(sc1);
            var results2 = _searcher.Search(sc2);

            Assert.AreNotEqual(results1.First().LongId, results2.First().LongId);
        }

        [Test]
        public void FluentApi_Standard_Results_Sorted_By_Score()
        {
            //Arrange
            var sc = _searcher.CreateSearchCriteria("content", SearchCriteria.BooleanOperation.Or);
            sc = sc.NodeName("umbraco").Or().Field("headerText", "umbraco").Or().Field("bodyText", "umbraco").Compile();

            //Act
            var results = _searcher.Search(sc);

            //Assert
            for (int i = 0; i < results.TotalItemCount - 1; i++)
            {
                var curr = results.ElementAt(i);
                var next = results.ElementAtOrDefault(i + 1);

                if (next == null)
                    break;

                Assert.IsTrue(curr.Score >= next.Score, string.Format("Result at index {0} must have a higher score than result at index {1}", i, i + 1));
            }
        }

        [Test]
        public void FluentApi_Skip_Results_Returns_Different_Results()
        {
            //Arrange
            var sc = _searcher.CreateSearchCriteria("content");
            sc = sc.Field("writerName", "administrator").Compile();

            //Act
            var results = _searcher.Search(sc);

            //Assert
            Assert.AreNotEqual(results.First(), results.Skip(2).First(), "Third result should be different");
        }

        [Test]
        public void FluentApiTests_Escaping_Includes_All_Words()
        {
            //Arrange
            var sc = _searcher.CreateSearchCriteria("content");
            var op = sc.NodeName("codegarden 09".Escape());
            sc = op.Compile();

            //Act
            var results = _searcher.Search(sc);

            //Assert
            Assert.IsTrue(results.TotalItemCount > 0);
        }

        [Test]
        public void FluentApiTests_Grouped_And_Examiness()
        {
            ////Arrange
            var criteria = _searcher.CreateSearchCriteria("content");

            //get all node type aliases starting with CWS and all nodees starting with "A"
            var filter = criteria.GroupedAnd(
                new string[] { "nodeTypeAlias", "nodeName" },
                new IExamineValue[] { "CWS".MultipleCharacterWildcard(), "A".MultipleCharacterWildcard() })
                .Compile();


            ////Act
            var results = _searcher.Search(filter);

            ////Assert
            Assert.IsTrue(results.TotalItemCount > 0);
        }

        [Test]
        public void FluentApiTests_Examiness_Proximity()
        {
            ////Arrange
            var criteria = _searcher.CreateSearchCriteria("content");

            //get all nodes that contain the words warren and creative within 5 words of each other
            var filter = criteria.Field("metaKeywords", "Warren creative".Proximity(5)).Compile();

            ////Act
            var results = _searcher.Search(filter);

            ////Assert
            Assert.IsTrue(results.TotalItemCount > 0);
        }

        [Test]
        public void FluentApiTests_Grouped_Or_Examiness()
        {
            ////Arrange
            var criteria = _searcher.CreateSearchCriteria("content");

            //get all node type aliases starting with CWS_Home OR and all nodees starting with "About"
            var filter = criteria.GroupedOr(
                new[] { "nodeTypeAlias", "nodeName" },
                new[] { "CWS\\_Home".Boost(10), "About".MultipleCharacterWildcard() })
                .Compile();


            ////Act
            var results = _searcher.Search(filter);

            ////Assert
            Assert.IsTrue(results.TotalItemCount > 0);
        }

        [Test]
        public void FluentApiTests_Cws_TextPage_OrderedByNodeName()
        {
            var criteria = _searcher.CreateSearchCriteria("content");
            IBooleanOperation query = criteria.NodeTypeAlias("cws_textpage");
            query = query.And().OrderBy("nodeName");
            var sCriteria = query.Compile();
            Console.WriteLine(sCriteria.ToString());
            var results = _searcher.Search(sCriteria);

			criteria = _searcher.CreateSearchCriteria("content");
            IBooleanOperation query2 = criteria.NodeTypeAlias("cws_textpage");
            query2 = query2.And().OrderByDescending("nodeName");
            var sCriteria2 = query2.Compile();
            Console.WriteLine(sCriteria2.ToString());
            var results2 = _searcher.Search(sCriteria2);

            Assert.AreNotEqual(results.First().LongId, results2.First().LongId);

        }

        private static ISearcher _searcher;
        private static IIndexer _indexer;
		private Lucene.Net.Store.Directory _luceneDir;

        #region Initialize and Cleanup


        [SetUp]
		public void TestSetup()
        {
			_luceneDir = new RAMDirectory();
			_indexer = IndexInitializer.GetUmbracoIndexer(_luceneDir);
            _indexer.RebuildIndex();
			_searcher = IndexInitializer.GetUmbracoSearcher(_luceneDir);
        }

        [TearDown]
		public void TestTearDown()
		{
			_luceneDir.Dispose();	
		}

        #endregion
    }
}
