using System;
using System.Collections.Generic;
using System.Xml.XPath;
using Umbraco.Core.Models;
using Umbraco.Core.Xml;

namespace Umbraco.Web
{
    /// <summary>
    /// Query methods used for accessing strongly typed content in templates
    /// </summary>
    public interface ITypedPublishedContentQuery
    {
        /// <summary>
        /// Gets a content item from the cache
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        IPublishedContent TypedContent(int id);

        /// <summary>
        /// Gets a content item from the cache
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        IPublishedContent TypedContent(Guid id);

        IPublishedContent TypedContentSingleAtXPath(string xpath, params XPathVariable[] vars);
        IEnumerable<IPublishedContent> TypedContent(IEnumerable<int> ids);
        IEnumerable<IPublishedContent> TypedContent(IEnumerable<Guid> ids);
        IEnumerable<IPublishedContent> TypedContentAtXPath(string xpath, params XPathVariable[] vars);
        IEnumerable<IPublishedContent> TypedContentAtXPath(XPathExpression xpath, params XPathVariable[] vars);
        IEnumerable<IPublishedContent> TypedContentAtRoot();

        // TODO: we CANNOT implement TypedMedia by Guid in v7 without break-changing IPublishedCache, since we don't support XPath navigation of the media tree.
        // surely there is a way we can support this without XPath, it's needed so we can query properly by UDI

        IPublishedContent TypedMedia(int id);
        //IPublishedContent TypedMedia(Guid id);
        IEnumerable<IPublishedContent> TypedMedia(IEnumerable<int> ids);
        //IEnumerable<IPublishedContent> TypedMedia(IEnumerable<Guid> ids);
        IEnumerable<IPublishedContent> TypedMediaAtRoot();

        /// <summary>
        /// Searches content
        /// </summary>
        /// <param name="term"></param>
        /// <param name="useWildCards"></param>
        /// <param name="searchProvider"></param>
        /// <returns></returns>
        IEnumerable<IPublishedContent> TypedSearch(string term, bool useWildCards = true, string searchProvider = null);

        /// <summary>
        /// Searches content
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <param name="totalRecords"></param>
        /// <param name="term"></param>
        /// <param name="useWildCards"></param>
        /// <param name="searchProvider"></param>
        /// <returns></returns>
        IEnumerable<IPublishedContent> TypedSearch(int skip, int take, out int totalRecords, string term, bool useWildCards = true, string searchProvider = null);

        /// <summary>
        /// Searhes content
        /// </summary>
        /// <param name="criteria"></param>
        /// <param name="searchProvider"></param>
        /// <returns></returns>
        IEnumerable<IPublishedContent> TypedSearch(Examine.SearchCriteria.ISearchCriteria criteria, Examine.Providers.BaseSearchProvider searchProvider = null);

        /// <summary>
        /// Searhes content
        /// </summary>
        /// <param name="criteria"></param>
        /// <param name="searchProvider"></param>
        /// <returns></returns>
        IEnumerable<IPublishedContent> TypedSearch(int skip, int take, out int totalrecords, Examine.SearchCriteria.ISearchCriteria criteria, Examine.Providers.BaseSearchProvider searchProvider = null);
    }
}