using Microsoft.Extensions.Logging;
using NPoco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Persistence.Repositories;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Persistence.Dtos;
using Umbraco.Cms.Infrastructure.Persistence.Factories;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Umbraco.Cms.Infrastructure.Persistence.Repositories.Implement;

/// <summary>
///     Represents a repository for doing CRUD operations for <see cref="IMemberType" />
/// </summary>
internal class MemberTypeRepository : ContentTypeRepositoryBase<IMemberType>, IMemberTypeRepository
{
    private readonly IShortStringHelper _shortStringHelper;

    public MemberTypeRepository(
        IScopeAccessor scopeAccessor,
        AppCaches cache,
        ILogger<MemberTypeRepository> logger,
        IContentTypeCommonRepository commonRepository,
        ILanguageRepository languageRepository,
        IShortStringHelper shortStringHelper)
        : base(scopeAccessor, cache, logger, commonRepository, languageRepository, shortStringHelper) =>
        _shortStringHelper = shortStringHelper;

    protected override bool SupportsPublishing => MemberType.SupportsPublishingConst;

    protected override Guid NodeObjectTypeId => Constants.ObjectTypes.MemberType;

    protected override IRepositoryCachePolicy<IMemberType, int> CreateCachePolicy() =>
        new FullDataSetRepositoryCachePolicy<IMemberType, int>(GlobalIsolatedCache, ScopeAccessor, GetEntityId, /*expires:*/ true);

    // every GetExists method goes cachePolicy.GetSomething which in turns goes PerformGetAll,
    // since this is a FullDataSet policy - and everything is cached
    // so here,
    // every PerformGet/Exists just GetMany() and then filters
    // except PerformGetAll which is the one really doing the job
    protected override IMemberType? PerformGet(int id)
        => GetMany().FirstOrDefault(x => x.Id == id);

    protected override IMemberType? PerformGet(Guid id)
        => GetMany().FirstOrDefault(x => x.Key == id);

    protected override IEnumerable<IMemberType> PerformGetAll(params Guid[]? ids)
    {
        IEnumerable<IMemberType> all = GetMany();
        return ids?.Any() ?? false ? all.Where(x => ids.Contains(x.Key)) : all;
    }

    protected override bool PerformExists(Guid id)
        => GetMany().FirstOrDefault(x => x.Key == id) != null;

    protected override IMemberType? PerformGet(string alias)
        => GetMany().FirstOrDefault(x => x.Alias.InvariantEquals(alias));

    protected override IEnumerable<IMemberType>? GetAllWithFullCachePolicy() =>
        CommonRepository.GetAllTypes()?.OfType<IMemberType>();

    protected override IEnumerable<IMemberType> PerformGetByQuery(IQuery<IMemberType> query)
    {
        Sql<ISqlContext> subQuery = GetSubquery();
        var translator = new SqlTranslator<IMemberType>(subQuery, query);
        Sql<ISqlContext> subSql = translator.Translate();
        Sql<ISqlContext> sql = GetBaseQuery(false)
            .WhereIn<NodeDto>(x => x.NodeId, subSql)
            .OrderBy<NodeDto>(x => x.SortOrder);
        var ids = Database.Fetch<int>(sql).Distinct().ToArray();

        return ids.Length > 0 ? GetMany(ids).OrderBy(x => x.Name) : Enumerable.Empty<IMemberType>();
    }

    protected override Sql<ISqlContext> GetBaseQuery(bool isCount)
    {
        if (isCount)
        {
            return Sql()
                .SelectCount()
                .From<NodeDto>()
                .InnerJoin<ContentTypeDto>().On<ContentTypeDto, NodeDto>(left => left.NodeId, right => right.NodeId)
                .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId);
        }

        Sql<ISqlContext> sql = Sql()
            .Select<NodeDto>(x => x.NodeId)
            .From<NodeDto>()
            .InnerJoin<ContentTypeDto>().On<ContentTypeDto, NodeDto>(left => left.NodeId, right => right.NodeId)
            .LeftJoin<PropertyTypeDto>().On<PropertyTypeDto, NodeDto>(left => left.ContentTypeId, right => right.NodeId)
            .LeftJoin<MemberPropertyTypeDto>()
            .On<MemberPropertyTypeDto, PropertyTypeDto>(left => left.PropertyTypeId, right => right.Id)
            .LeftJoin<DataTypeDto>().On<DataTypeDto, PropertyTypeDto>(left => left.NodeId, right => right.DataTypeId)
            .LeftJoin<PropertyTypeGroupDto>()
            .On<PropertyTypeGroupDto, NodeDto>(left => left.ContentTypeNodeId, right => right.NodeId)
            .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId);

        return sql;
    }

    protected Sql<ISqlContext> GetSubquery()
    {
        Sql<ISqlContext> sql = Sql()
            .Select("DISTINCT(umbracoNode.id)")
            .From<NodeDto>()
            .InnerJoin<ContentTypeDto>().On<ContentTypeDto, NodeDto>(left => left.NodeId, right => right.NodeId)
            .LeftJoin<PropertyTypeDto>().On<PropertyTypeDto, NodeDto>(left => left.ContentTypeId, right => right.NodeId)
            .LeftJoin<MemberPropertyTypeDto>()
            .On<MemberPropertyTypeDto, PropertyTypeDto>(left => left.PropertyTypeId, right => right.Id)
            .LeftJoin<DataTypeDto>().On<DataTypeDto, PropertyTypeDto>(left => left.NodeId, right => right.DataTypeId)
            .LeftJoin<PropertyTypeGroupDto>()
            .On<PropertyTypeGroupDto, NodeDto>(left => left.ContentTypeNodeId, right => right.NodeId)
            .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId);
        return sql;
    }

    protected override string GetBaseWhereClause() => $"{Constants.DatabaseSchema.Tables.Node}.id = @id";

    protected override IEnumerable<string> GetDeleteClauses()
    {
        var l = (List<string>)base.GetDeleteClauses(); // we know it's a list
        l.Add("DELETE FROM cmsMemberType WHERE NodeId = @id");
        l.Add("DELETE FROM cmsContentType WHERE nodeId = @id");
        l.Add("DELETE FROM umbracoNode WHERE id = @id");
        return l;
    }

    protected override void PersistNewItem(IMemberType entity)
    {
        ValidateAlias(entity);

        entity.AddingEntity();

        // set a default icon if one is not specified
        if (entity.Icon.IsNullOrWhiteSpace())
        {
            entity.Icon = Constants.Icons.Member;
        }

        PersistNewBaseContentType(entity);

        // Handles the MemberTypeDto (cmsMemberType table)
        IEnumerable<MemberPropertyTypeDto> memberTypeDtos = ContentTypeFactory.BuildMemberPropertyTypeDtos(entity);
        foreach (MemberPropertyTypeDto memberTypeDto in memberTypeDtos)
        {
            Database.Insert(memberTypeDto);
        }

        entity.ResetDirtyProperties();
    }

    protected override void PersistUpdatedItem(IMemberType entity)
    {
        ValidateAlias(entity);

        // Updates Modified date
        entity.UpdatingEntity();

        // Look up parent to get and set the correct Path if ParentId has changed
        if (entity.IsPropertyDirty("ParentId"))
        {
            NodeDto? parent = Database.First<NodeDto>("WHERE id = @ParentId", new { entity.ParentId });
            entity.Path = string.Concat(parent.Path, ",", entity.Id);
            entity.Level = parent.Level + 1;
            var maxSortOrder =
                Database.ExecuteScalar<int>(
                    "SELECT coalesce(max(sortOrder),0) FROM umbracoNode WHERE parentid = @ParentId AND nodeObjectType = @NodeObjectType",
                    new { entity.ParentId, NodeObjectType = NodeObjectTypeId });
            entity.SortOrder = maxSortOrder + 1;
        }

        PersistUpdatedBaseContentType(entity);

        // remove and insert - handle cmsMemberType table
        Database.Delete<MemberPropertyTypeDto>("WHERE NodeId = @Id", new { entity.Id });
        IEnumerable<MemberPropertyTypeDto> memberTypeDtos = ContentTypeFactory.BuildMemberPropertyTypeDtos(entity);
        foreach (MemberPropertyTypeDto memberTypeDto in memberTypeDtos)
        {
            Database.Insert(memberTypeDto);
        }

        entity.ResetDirtyProperties();
    }
}
