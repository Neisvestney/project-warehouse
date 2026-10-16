namespace ProjectWarehouse.Server.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

public class ListUpdater : IListUpdater
{
    private readonly IMapper _mapper;

    public ListUpdater(IMapper mapper)
    {
        _mapper = mapper;
    }
    
    public void UpdateList<T, TDto>(IList<TDto>? dto, IList<T>? source, DbSet<T> dbSet, Action<TDto, T>? afterMap = null) 
        where T : class
        where TDto : class
    {
        if (dto != null && source != null)
        {
            for (int i = 0; i < dto.Count; i++)
            {
                if (i < source.Count)
                {
                    _mapper.Map(dto[i], source[i]);
                    afterMap?.Invoke(dto[i], source[i]);
                }
                
                else
                {
                    var newSubEntity = _mapper.Map<T>(dto[i]);
                    afterMap?.Invoke(dto[i], newSubEntity);
                    source.Add(newSubEntity);
                }
            }

            // Remove extra entities if DTO has fewer entities than the current list
            while (source.Count > dto.Count)
            {
                dbSet.Remove(source[^1]);
                source.RemoveAt(source.Count - 1);
            }
        }
    }

    public async Task UpdateListAsync<T, TDto>(IList<TDto>? dto, IList<T>? source, DbSet<T> dbSet, Func<TDto, T, Task>? afterMapAsync = null) where T : class where TDto : class
    {
        if (dto != null && source != null)
        {
            for (int i = 0; i < dto.Count; i++)
            {
                if (i < source.Count)
                {
                    _mapper.Map(dto[i], source[i]);
                    if (afterMapAsync != null)
                    {
                        await afterMapAsync(dto[i], source[i]);
                    }
                    
                }
                
                else
                {
                    var newSubEntity = _mapper.Map<T>(dto[i]);
                    if (afterMapAsync != null)
                    {
                        await afterMapAsync(dto[i], newSubEntity);
                    }
                    source.Add(newSubEntity);
                }
            }

            // Remove extra entities if DTO has fewer entities than the current list
            while (source.Count > dto.Count)
            {
                dbSet.Remove(source[^1]);
                source.RemoveAt(source.Count - 1);
            }
        }
    }
    
    // UpdateList((source, dto) => source.Id == dto.Id, dto =>> dto.Id == 0, dtoList, sourceList, dbSet)
    public void UpdateList<T, TDto>(List<TDto>? dto, List<T>? source, DbSet<T> dbSet, Func<T, TDto, bool> compare, Func<TDto, bool> isNew,
        Action<TDto, T>? afterMap = null) where T : class where TDto : class
    {
        if (dto == null || source == null) return;

        for (var i = source.Count - 1; i >= 0; i--)
        {
            var itemDto = dto.FirstOrDefault(x => compare(source[i], x));
            if (itemDto != null) continue;

            dbSet.Remove(source[i]);
            source.RemoveAt(i);
        }

        foreach (var itemDto in dto)
        {
            if (isNew(itemDto))
            {
                var newSubEntity = _mapper.Map<T>(itemDto);
                afterMap?.Invoke(itemDto, newSubEntity);
                source.Add(newSubEntity);
                continue;
            }

            var item = source.FirstOrDefault(x => compare(x, itemDto));
            if (item != null)
            {
                _mapper.Map(itemDto, item);
                afterMap?.Invoke(itemDto, item);
            }
            else
            {
                var newSubEntity = _mapper.Map<T>(itemDto);
                afterMap?.Invoke(itemDto, newSubEntity);
                source.Add(newSubEntity);
            }
        }
    }
    
    public async Task UpdateListAsync<T, TDto>(List<TDto>? dto, List<T>? source, DbSet<T> dbSet, Func<T, TDto, bool> compare, Func<TDto, bool> isNew,
        Func<TDto, T, Task>? afterMapAsync = null) where T : class where TDto : class
    {
        if (dto == null || source == null) return;

        for (var i = source.Count - 1; i >= 0; i--)
        {
            var itemDto = dto.FirstOrDefault(x => compare(source[i], x));
            if (itemDto != null) continue;

            dbSet.Remove(source[i]);
            source.RemoveAt(i);
        }

        foreach (var itemDto in dto)
        {
            if (isNew(itemDto))
            {
                var newSubEntity = _mapper.Map<T>(itemDto);
                if (afterMapAsync != null) {
                    await afterMapAsync(itemDto, newSubEntity);   
                }
                source.Add(newSubEntity);
                continue;
            }

            var item = source.FirstOrDefault(x => compare(x, itemDto));
            if (item != null)
            {
                _mapper.Map(itemDto, item);
                if (afterMapAsync != null) {
                    await afterMapAsync(itemDto, item);   
                }
            }
            else
            {
                var newSubEntity = _mapper.Map<T>(itemDto);
                if (afterMapAsync != null) {
                    await afterMapAsync(itemDto, newSubEntity);   
                }
                source.Add(newSubEntity);
            }
        }
    }
}

public interface IListUpdater
{
    public void UpdateList<T, TDto>(IList<TDto>? dto, IList<T>? source, DbSet<T> dbSet, Action<TDto, T>? afterMap = null)
        where T : class
        where TDto : class;  
    public Task UpdateListAsync<T, TDto>(IList<TDto>? dto, IList<T>? source, DbSet<T> dbSet, Func<TDto, T, Task>? afterMapAsync = null)
        where T : class
        where TDto : class;

    public void UpdateList<T, TDto>(List<TDto>? dto, List<T>? source, DbSet<T> dbSet, Func<T, TDto, bool> compare, Func<TDto, bool> isNew,
        Action<TDto, T>? afterMap = null)
        where T : class 
        where TDto : class;

    public Task UpdateListAsync<T, TDto>(List<TDto>? dto, List<T>? source, DbSet<T> dbSet, Func<T, TDto, bool> compare, Func<TDto, bool> isNew,
        Func<TDto, T, Task>? afterMapAsync = null)
        where T : class
        where TDto : class;
}