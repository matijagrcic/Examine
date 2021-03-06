﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using Examine.LuceneEngine.Config;
using Examine.LuceneEngine.SearchCriteria;
using Examine.Providers;
using Examine.SearchCriteria;
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;

namespace Examine.LuceneEngine.Providers
{
    ///<summary>
    /// A provider that allows for searching across multiple indexes
    ///</summary>
    public class MultiIndexSearcher : BaseLuceneSearcher
    {

        #region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
        public MultiIndexSearcher()
		{
        }

        /// <summary>
        /// Constructor to allow for creating an indexer at runtime
        /// </summary>
        /// <param name="indexPath"></param>
        /// <param name="analyzer"></param>
		[SecuritySafeCritical]
		public MultiIndexSearcher(IEnumerable<DirectoryInfo> indexPath, Analyzer analyzer)
            : base(analyzer)
        {
	        var searchers = new List<LuceneSearcher>();
			//NOTE: DO NOT convert to Linq like this used to be as this breaks security level 2 code because of something Linq is doing.
			foreach (var ip in indexPath)
			{
				searchers.Add(new LuceneSearcher(ip, IndexingAnalyzer));
			}
	        Searchers = searchers;
        }

		/// <summary>
		/// Constructor to allow for creating an indexer at runtime
		/// </summary>
		/// <param name="luceneDirs"></param>
		/// <param name="analyzer"></param>
		[SecuritySafeCritical]
		public MultiIndexSearcher(IEnumerable<Lucene.Net.Store.Directory> luceneDirs, Analyzer analyzer)
			: base(analyzer)
		{
			var searchers = new List<LuceneSearcher>();
			//NOTE: DO NOT convert to Linq like this used to be as this breaks security level 2 code because of something Linq is doing.
			foreach (var luceneDirectory in luceneDirs)
			{
				searchers.Add(new LuceneSearcher(luceneDirectory, IndexingAnalyzer));
			}
			Searchers = searchers;
		}

		#endregion
        
	    ///<summary>
	    /// The underlying LuceneSearchers that will be searched across
	    ///</summary>
	    public IEnumerable<LuceneSearcher> Searchers
		{
			[SecuritySafeCritical]
		    get;
			[SecuritySafeCritical]
			private set;
	    }

        [SecuritySafeCritical]
        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);

            //need to check if the index set is specified, if it's not, we'll see if we can find one by convension
            //if the folder is not null and the index set is null, we'll assume that this has been created at runtime.
            if (config["indexSets"] == null)
            {
                throw new ArgumentNullException("indexSets on MultiIndexSearcher provider has not been set in configuration");
            }

            var toSearch = new List<IndexSet>();
            var sets = IndexSets.Instance.Sets.Cast<IndexSet>();
            foreach(var i in config["indexSets"].Split(','))
            {
                var s = sets.Where(x => x.SetName == i).SingleOrDefault();
                if (s == null)
                {
                    throw new ArgumentException("The index set " + i + " does not exist");
                }
                toSearch.Add(s);
            }

            //create the searchers
            var analyzer = IndexingAnalyzer;
            var searchers = new List<LuceneSearcher>();
            //DO NOT PUT THIS INTO LINQ BECAUSE THE SECURITY ACCCESS SHIT WONT WORK
            foreach (var s in toSearch)
            {
                searchers.Add(new LuceneSearcher(s.IndexDirectory, analyzer));
            }
            Searchers = searchers;
        }
        
        /// <summary>
        /// Returns a list of fields to search on based on all distinct fields found in the sub searchers
        /// </summary>
        /// <returns></returns>
        protected override internal string[] GetSearchFields()
        {
            var searchableFields = new List<string>();
            foreach (var searcher in Searchers)
            {
                searchableFields.AddRange(searcher.GetSearchFields());
            }
            return searchableFields.Distinct().ToArray();
        }

        /// <summary>
        /// Gets the searcher for this instance
        /// </summary>
        /// <returns></returns>
		[SecuritySafeCritical]
        public override Searcher GetSearcher()
        {
	        var searchables = new List<Searchable>();
			//NOTE: Do not convert this to Linq as it will fail the Code Analysis because Linq screws with it.
			foreach(var s in Searchers)
			{
				searchables.Add(s.GetSearcher());
			}
			return new MultiSearcher(searchables.ToArray());
        }

     
    }
}
