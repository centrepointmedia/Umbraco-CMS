﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LightInject;
using Moq;
using NPoco;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Mappers;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Persistence.UnitOfWork;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;
using Umbraco.Core.Strings;
using Umbraco.Web.Services;

namespace Umbraco.Tests.TestHelpers
{
    /// <summary>
    /// Provides objects for tests.
    /// </summary>
    internal partial class TestObjects
    {
        private readonly IServiceContainer _container;

        public TestObjects(IServiceContainer container)
        {
            _container = container;
        }

        /// <summary>
        /// Gets the default ISqlSyntaxProvider objects.
        /// </summary>
        /// <param name="logger">A logger.</param>
        /// <param name="lazyScopeProvider">A (lazy) scope provider.</param>
        /// <returns>The default ISqlSyntaxProvider objects.</returns>
        public IEnumerable<ISqlSyntaxProvider> GetDefaultSqlSyntaxProviders(ILogger logger, Lazy<IScopeProvider> lazyScopeProvider = null)
        {
            return new ISqlSyntaxProvider[]
            {
                new MySqlSyntaxProvider(logger),
                new SqlCeSyntaxProvider(),
                new SqlServerSyntaxProvider(lazyScopeProvider ?? new Lazy<IScopeProvider>(() => null))
            };
        }

        /// <summary>
        /// Gets an UmbracoDatabase.
        /// </summary>
        /// <param name="logger">A logger.</param>
        /// <returns>An UmbracoDatabase.</returns>
        /// <remarks>This is just a void database that has no actual database but pretends to have an open connection
        /// that can begin a transaction.</remarks>
        public UmbracoDatabase GetUmbracoSqlCeDatabase(ILogger logger)
        {
            var syntax = new SqlCeSyntaxProvider();
            var connection = GetDbConnection();
            var sqlContext = new SqlContext(syntax, Mock.Of<IPocoDataFactory>(), DatabaseType.SQLCe);
            return new UmbracoDatabase(connection, sqlContext, logger);
        }

        /// <summary>
        /// Gets an UmbracoDatabase.
        /// </summary>
        /// <param name="logger">A logger.</param>
        /// <returns>An UmbracoDatabase.</returns>
        /// <remarks>This is just a void database that has no actual database but pretends to have an open connection
        /// that can begin a transaction.</remarks>
        public UmbracoDatabase GetUmbracoSqlServerDatabase(ILogger logger)
        {
            var syntax = new SqlServerSyntaxProvider(new Lazy<IScopeProvider>(() => null)); // do NOT try to get the server's version!
            var connection = GetDbConnection();
            var sqlContext = new SqlContext(syntax, Mock.Of<IPocoDataFactory>(), DatabaseType.SqlServer2008);
            return new UmbracoDatabase(connection, sqlContext, logger);
        }

        public void RegisterServices(IServiceContainer container)
        { }

        /// <summary>
        /// Gets a ServiceContext.
        /// </summary>
        /// <param name="repositoryFactory">A repository factory.</param>
        /// <param name="dbUnitOfWorkProvider">A database unit of work provider.</param>
        /// <param name="cache">A cache.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="eventMessagesFactory">An event messages factory.</param>
        /// <param name="urlSegmentProviders">Some url segment providers.</param>
        /// <param name="container">A container.</param>
        /// <returns>A ServiceContext.</returns>
        /// <remarks>Should be used sparingly for integration tests only - for unit tests
        /// just mock the services to be passed to the ctor of the ServiceContext.</remarks>
        public ServiceContext GetServiceContext(RepositoryFactory repositoryFactory,
            IScopeUnitOfWorkProvider dbUnitOfWorkProvider,
            CacheHelper cache,
            ILogger logger,
            IEventMessagesFactory eventMessagesFactory,
            IEnumerable<IUrlSegmentProvider> urlSegmentProviders,
            IServiceFactory container = null)
        {
            if (repositoryFactory == null) throw new ArgumentNullException(nameof(repositoryFactory));
            if (dbUnitOfWorkProvider == null) throw new ArgumentNullException(nameof(dbUnitOfWorkProvider));
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (eventMessagesFactory == null) throw new ArgumentNullException(nameof(eventMessagesFactory));

            var provider = dbUnitOfWorkProvider;
            var mediaFileSystem = new MediaFileSystem(Mock.Of<IFileSystem>());

            var migrationEntryService = GetLazyService<IMigrationEntryService>(container, () => new MigrationEntryService(provider, logger, eventMessagesFactory));
            var externalLoginService = GetLazyService<IExternalLoginService>(container, () => new ExternalLoginService(provider, logger, eventMessagesFactory));
            var publicAccessService = GetLazyService<IPublicAccessService>(container, () => new PublicAccessService(provider, logger, eventMessagesFactory));
            var taskService = GetLazyService<ITaskService>(container, () => new TaskService(provider, logger, eventMessagesFactory));
            var domainService = GetLazyService<IDomainService>(container, () => new DomainService(provider, logger, eventMessagesFactory));
            var auditService = GetLazyService<IAuditService>(container, () => new AuditService(provider, logger, eventMessagesFactory));

            var localizedTextService = GetLazyService<ILocalizedTextService>(container, () => new LocalizedTextService(
                    new Lazy<LocalizedTextServiceFileSources>(() =>
                    {
                        var mainLangFolder = new DirectoryInfo(IOHelper.MapPath(SystemDirectories.Umbraco + "/config/lang/"));
                        var appPlugins = new DirectoryInfo(IOHelper.MapPath(SystemDirectories.AppPlugins));
                        var configLangFolder = new DirectoryInfo(IOHelper.MapPath(SystemDirectories.Config + "/lang/"));

                        var pluginLangFolders = appPlugins.Exists == false
                            ? Enumerable.Empty<LocalizedTextServiceSupplementaryFileSource>()
                            : appPlugins.GetDirectories()
                                .SelectMany(x => x.GetDirectories("Lang"))
                                .SelectMany(x => x.GetFiles("*.xml", SearchOption.TopDirectoryOnly))
                                .Where(x => Path.GetFileNameWithoutExtension(x.FullName).Length == 5)
                                .Select(x => new LocalizedTextServiceSupplementaryFileSource(x, false));

                        //user defined langs that overwrite the default, these should not be used by plugin creators
                        var userLangFolders = configLangFolder.Exists == false
                            ? Enumerable.Empty<LocalizedTextServiceSupplementaryFileSource>()
                            : configLangFolder
                                .GetFiles("*.user.xml", SearchOption.TopDirectoryOnly)
                                .Where(x => Path.GetFileNameWithoutExtension(x.FullName).Length == 10)
                                .Select(x => new LocalizedTextServiceSupplementaryFileSource(x, true));

                        return new LocalizedTextServiceFileSources(
                            logger,
                            cache.RuntimeCache,
                            mainLangFolder,
                            pluginLangFolders.Concat(userLangFolders));

                    }),
                    logger));

            var userService = GetLazyService<IUserService>(container, () => new UserService(provider, logger, eventMessagesFactory));
            var dataTypeService = GetLazyService<IDataTypeService>(container, () => new DataTypeService(provider, logger, eventMessagesFactory));
            var contentService = GetLazyService<IContentService>(container, () => new ContentService(provider, logger, eventMessagesFactory, mediaFileSystem));
            var notificationService = GetLazyService<INotificationService>(container, () => new NotificationService(provider, userService.Value, contentService.Value, logger));
            var serverRegistrationService = GetLazyService<IServerRegistrationService>(container, () => new ServerRegistrationService(provider, logger, eventMessagesFactory));
            var memberGroupService = GetLazyService<IMemberGroupService>(container, () => new MemberGroupService(provider, logger, eventMessagesFactory));
            var memberService = GetLazyService<IMemberService>(container, () => new MemberService(provider, logger, eventMessagesFactory, memberGroupService.Value, mediaFileSystem));
            var mediaService = GetLazyService<IMediaService>(container, () => new MediaService(provider, mediaFileSystem, logger, eventMessagesFactory));
            var contentTypeService = GetLazyService<IContentTypeService>(container, () => new ContentTypeService(provider, logger, eventMessagesFactory, contentService.Value));
            var mediaTypeService = GetLazyService<IMediaTypeService>(container, () => new MediaTypeService(provider, logger, eventMessagesFactory, mediaService.Value));
            var fileService = GetLazyService<IFileService>(container, () => new FileService(provider, logger, eventMessagesFactory));
            var localizationService = GetLazyService<ILocalizationService>(container, () => new LocalizationService(provider, logger, eventMessagesFactory));

            var memberTypeService = GetLazyService<IMemberTypeService>(container, () => new MemberTypeService(provider, logger, eventMessagesFactory, memberService.Value));
            var entityService = GetLazyService<IEntityService>(container, () => new EntityService(
                    provider, logger, eventMessagesFactory,
                    contentService.Value, contentTypeService.Value, mediaService.Value, mediaTypeService.Value, dataTypeService.Value, memberService.Value, memberTypeService.Value,
                    //TODO: Consider making this an isolated cache instead of using the global one
                    cache.RuntimeCache));

            var macroService = GetLazyService<IMacroService>(container, () => new MacroService(provider, logger, eventMessagesFactory));
            var packagingService = GetLazyService<IPackagingService>(container, () => new PackagingService(logger, contentService.Value, contentTypeService.Value, mediaService.Value, macroService.Value, dataTypeService.Value, fileService.Value, localizationService.Value, entityService.Value, userService.Value, provider, urlSegmentProviders));
            var relationService = GetLazyService<IRelationService>(container, () => new RelationService(provider, logger, eventMessagesFactory, entityService.Value));
            var treeService = GetLazyService<IApplicationTreeService>(container, () => new ApplicationTreeService(logger, cache));
            var tagService = GetLazyService<ITagService>(container, () => new TagService(provider, logger, eventMessagesFactory));
            var sectionService = GetLazyService<ISectionService>(container, () => new SectionService(userService.Value, treeService.Value, provider, cache));
            var redirectUrlService = GetLazyService<IRedirectUrlService>(container, () => new RedirectUrlService(provider, logger, eventMessagesFactory));

            return new ServiceContext(
                migrationEntryService,
                publicAccessService,
                taskService,
                domainService,
                auditService,
                localizedTextService,
                tagService,
                contentService,
                userService,
                memberService,
                mediaService,
                contentTypeService,
                mediaTypeService,
                dataTypeService,
                fileService,
                localizationService,
                packagingService,
                serverRegistrationService,
                entityService,
                relationService,
                treeService,
                sectionService,
                macroService,
                memberTypeService,
                memberGroupService,
                notificationService,
                externalLoginService,
                redirectUrlService);
        }

        private Lazy<T> GetLazyService<T>(IServiceFactory container, Func<T> ctor)
            where T : class
        {
            return new Lazy<T>(() => container?.TryGetInstance<T>() ?? ctor());
        }

        public IScopeProvider GetScopeProvider(ILogger logger, IUmbracoDatabaseFactory databaseFactory = null)
        {
            if (databaseFactory == null)
            {
                //var mappersBuilder = new MapperCollectionBuilder(Current.Container); // fixme
                //mappersBuilder.AddCore();
                //var mappers = mappersBuilder.CreateCollection();
                var mappers = Current.Container.GetInstance<IMapperCollection>();
                databaseFactory = new UmbracoDatabaseFactory(Constants.System.UmbracoConnectionName, GetDefaultSqlSyntaxProviders(logger), logger, mappers);
            }

            var fileSystems = new FileSystems(logger);
            var scopeProvider = new ScopeProvider(databaseFactory, fileSystems, logger);
            return scopeProvider;
        }

        public IScopeUnitOfWorkProvider GetScopeUnitOfWorkProvider(ILogger logger, IUmbracoDatabaseFactory databaseFactory = null, RepositoryFactory repositoryFactory = null, IScopeProvider scopeProvider = null)
        {
            if (databaseFactory == null)
            {
                //var mappersBuilder = new MapperCollectionBuilder(Current.Container); // fixme
                //mappersBuilder.AddCore();
                //var mappers = mappersBuilder.CreateCollection();
                var mappers = Current.Container.GetInstance<IMapperCollection>();
                databaseFactory = new UmbracoDatabaseFactory(Constants.System.UmbracoConnectionName, GetDefaultSqlSyntaxProviders(logger), logger, mappers);
            }
            if (scopeProvider == null)
            {
                var fileSystems = new FileSystems(logger);
                scopeProvider = new ScopeProvider(databaseFactory, fileSystems, logger);
            }
            repositoryFactory = repositoryFactory  ??  new RepositoryFactory(Mock.Of<IServiceContainer>());
            IDatabaseContext databaseContext = databaseFactory;
            return new ScopeUnitOfWorkProvider(scopeProvider, databaseContext, repositoryFactory);
        }
    }
}