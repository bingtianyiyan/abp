﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.IdentityServer.Devices;
using Volo.Abp.MongoDB;

namespace Volo.Abp.IdentityServer.MongoDB;

public class MongoDeviceFlowCodesRepository :
    MongoDbRepository<IAbpIdentityServerMongoDbContext, DeviceFlowCodes, Guid>, IDeviceFlowCodesRepository
{
    public MongoDeviceFlowCodesRepository(
        IMongoDbContextProvider<IAbpIdentityServerMongoDbContext> dbContextProvider) : base(dbContextProvider)
    {

    }

    public virtual async Task<DeviceFlowCodes> FindByUserCodeAsync(
        string userCode,
        CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync(cancellationToken))
            .Where(d => d.UserCode == userCode)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<DeviceFlowCodes> FindByDeviceCodeAsync(string deviceCode, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync(cancellationToken))
            .Where(d => d.DeviceCode == deviceCode)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<DeviceFlowCodes>> GetListByExpirationAsync(
        DateTime maxExpirationDate,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync(cancellationToken))
            .Where(x => x.Expiration != null && x.Expiration < maxExpirationDate)
            .OrderBy(x => x.ClientId)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task DeleteExpirationAsync(DateTime maxExpirationDate, CancellationToken cancellationToken = default)
    {
        await DeleteDirectAsync(x => x.Expiration != null && x.Expiration < maxExpirationDate, cancellationToken: cancellationToken);
    }
}
