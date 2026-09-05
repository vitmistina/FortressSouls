local json = require('json')

local SCHEMA_VERSION = 'fortress-souls-dwarf-surroundings.v0.2'
local CURRENT_SCENE_SCHEMA_VERSION = 'fortress-souls-dwarf-surroundings.v0.2.1'
local DEFAULT_RADIUS = 1
-- Keep the process output bounded even if the application is misconfigured.
local MAX_RADIUS = 16
local MAX_UNITS_SCANNED = 10000
local CURRENT_LOCAL_SIZE = 33
local CURRENT_SITE_WIDTH = 24
local CURRENT_SITE_HEIGHT = 12
local CURRENT_MAX_DETAILS = 24
local CURRENT_MAX_ITEMS_SCANNED = 200000

local function safe(fn)
    local ok, value = pcall(fn)
    if ok then return value, nil end
    return nil, tostring(value)
end

local function scalar(value)
    local kind = type(value)
    if kind == 'string' or kind == 'number' or kind == 'boolean' then return value end
    return nil
end

local function walkable_flag(value)
    local scalar_value = scalar(value)
    if type(scalar_value) == 'boolean' then return scalar_value end
    if type(scalar_value) == 'number' then return scalar_value > 0 end
    return nil
end

local function enum_name(enum, value)
    if enum == nil or value == nil then return nil end
    return scalar(select(1, safe(function() return enum[value] end)))
end

local function integer(value, name, minimum, maximum)
    local parsed = tonumber(value)
    if parsed == nil or parsed ~= math.floor(parsed) then
        return nil, name .. ' must be an integer.'
    end
    if parsed < minimum or parsed > maximum then
        return nil, name .. ' must be between ' .. minimum .. ' and ' .. maximum .. '.'
    end
    return parsed, nil
end

local function position(value)
    if value == nil then return nil end
    local x = scalar(select(1, safe(function() return value.x end)))
    local y = scalar(select(1, safe(function() return value.y end)))
    local z = scalar(select(1, safe(function() return value.z end)))
    if x == nil or y == nil or z == nil or x == -30000 then return nil end
    return {x=x, y=y, z=z}
end

local function read_position(fn)
    local ok, first, second, third = pcall(fn)
    if not ok then return nil end
    if type(first) == 'number' then
        if first == -30000 or type(second) ~= 'number' or type(third) ~= 'number' then return nil end
        return {x=first, y=second, z=third}
    end
    return position(first)
end

local function key(pos)
    return pos.x .. ',' .. pos.y .. ',' .. pos.z
end

local function emit(value)
    print(json.encode(value, {pretty=false}))
end

local function failure(code, message)
    emit({schemaVersion=SCHEMA_VERSION, error={code=code, message=message}})
end

local function find_unit(unit_id)
    local unit = select(1, safe(function() return df.unit.find(unit_id) end))
    if unit == nil then return nil end
    return unit
end

local function validate_bounds(center, radius)
    local bounds = {
        x1=center.x-radius,
        y1=center.y-radius,
        z=center.z,
        x2=center.x+radius,
        y2=center.y+radius,
        width=(radius*2)+1,
        height=(radius*2)+1
    }

    for y=bounds.y1,bounds.y2 do
        for x=bounds.x1,bounds.x2 do
            local pos = {x=x, y=y, z=bounds.z}
            if not dfhack.maps.isValidTilePos(pos)
               or dfhack.maps.getTileType(pos) == nil
               or dfhack.maps.getTileBlock(pos) == nil then
                return nil, 'Requested surroundings contain unavailable map tiles.'
            end
        end
    end

    return bounds, nil
end

local function terrain_class(attrs, has_building)
    if has_building then return 'building' end
    if attrs == nil then return nil end

    local shape = enum_name(df.tiletype_shape, attrs.shape)
    if shape == 'WALL' or shape == 'FORTIFICATION' then return 'wall' end
    if shape == 'RAMP' or shape == 'RAMP_TOP' then return 'ramp' end
    if shape == 'FLOOR' or shape == 'BOULDER' or shape == 'BROOK_TOP' then return 'floor' end
    if shape ~= nil and string.find(shape, 'STAIR', 1, true) then return 'floor' end

    local shape_attrs = df.tiletype_shape.attrs[attrs.shape]
    local walkable = shape_attrs and walkable_flag(shape_attrs.walkable) or nil
    if walkable == true then return 'floor' end

    return nil
end

local function index_unit_counts(bounds, warnings)
    local counts = {}
    local units = dfhack.units.getUnitsInBox(bounds.x1, bounds.y1, bounds.z, bounds.x2, bounds.y2, bounds.z) or {}

    for unit_index, unit in ipairs(units) do
        if unit_index > MAX_UNITS_SCANNED then
            table.insert(warnings, 'Unit scan stopped at the configured limit; visible unit counts may be incomplete.')
            break
        end

        local pos = read_position(function() return dfhack.units.getPosition(unit) end)
        if pos ~= nil and pos.z == bounds.z and pos.x >= bounds.x1 and pos.x <= bounds.x2 and pos.y >= bounds.y1 and pos.y <= bounds.y2 then
            local cell_key = key(pos)
            counts[cell_key] = (counts[cell_key] or 0) + 1
        end
    end

    return counts
end

local function append_limited(list, value, limit)
    if #list < limit then table.insert(list, value) end
end

local function terrain_info(pos)
    local tiletype = select(1, safe(function() return dfhack.maps.getTileType(pos) end))
    local attrs = tiletype and df.tiletype.attrs[tiletype] or nil
    local shape = attrs and enum_name(df.tiletype_shape, attrs.shape) or nil
    local material = attrs and enum_name(df.tiletype_material, attrs.material) or nil
    local flags = select(1, safe(function() return dfhack.maps.getTileFlags(pos) end))
    local flow_size = flags and scalar(flags.flow_size) or 0
    local liquid_type = nil
    if flow_size > 0 then liquid_type = flags.liquid_type and 'MAGMA' or 'WATER' end
    return {
        tiletype=enum_name(df.tiletype, tiletype), shape=shape, material=material,
        hidden=flags ~= nil and flags.hidden == true,
        outside=flags ~= nil and flags.outside == true,
        light=flags ~= nil and flags.light == true,
        subterranean=flags ~= nil and flags.subterranean == true,
        flowSize=flow_size, liquidType=liquid_type,
        building=select(1, safe(function() return dfhack.buildings.findAtTile(pos) end)),
        zones=select(1, safe(function() return dfhack.buildings.findCivzonesAt(pos) end)) or {},
        plant=select(1, safe(function() return dfhack.maps.getPlantAtTile(pos) end)),
        construction=select(1, safe(function() return dfhack.constructions.findAtTile(pos) end))
    }
end

local function current_bounds(center, width, height)
    local x_radius = math.floor((width - 1) / 2)
    local y_radius = math.floor((height - 1) / 2)
    return {
        x1=center.x-x_radius, y1=center.y-y_radius, z=center.z,
        x2=center.x+(width-x_radius-1), y2=center.y+(height-y_radius-1),
        width=width, height=height
    }
end

local function material_symbol(material)
    if material == 'GRASS' then return 'g' end
    if material == 'SOIL' then return 's' end
    if material == 'STONE' or material == 'MINERAL' then return 'r' end
    if material == 'WOOD' then return 'w' end
    if material == 'METAL' then return 'm' end
    if material == 'ICE' then return 'i' end
    if material == 'CLOTH' then return 'c' end
    if material ~= nil then return 'o' end
    return '?'
end

local function terrain_symbol(info)
    if info.hidden then return '?' end
    if info.shape == 'WALL' or info.shape == 'FORTIFICATION' then return '#' end
    if info.shape == 'RAMP' or info.shape == 'RAMP_TOP' then return '^' end
    if info.shape == 'STAIR_UP' then return '<' end
    if info.shape == 'STAIR_DOWN' then return '>' end
    if info.shape == 'STAIR_UPDOWN' then return 'X' end
    if info.shape == 'FLOOR' or info.shape == 'BOULDER' or info.shape == 'BROOK_TOP' then return '.' end
    if info.shape == 'EMPTY' then return ' ' end
    return '?'
end

local function feature_symbol(info)
    if info.hidden then return ' ' end
    if info.liquidType == 'MAGMA' then return 'M' end
    if info.liquidType == 'WATER' then return '~' end
    if info.building ~= nil then
        local building_type = scalar(select(1, safe(function() return info.building:getType() end)))
        local building_name = enum_name(df.building_type, building_type)
        if building_name == 'WAGON' or building_type == 32 then return 'W' end
        return 'B'
    end
    if info.construction ~= nil then return 'C' end
    if info.plant ~= nil then return 'T' end
    return ' '
end

local function environment_label(flags)
    if flags.subterranean then return 'underground' end
    if flags.outside then return 'above_ground_outdoors' end
    if not flags.outside then return 'above_ground_sheltered' end
    return 'unknown'
end

local function surface_position(x, y, map_size)
    for z=map_size.z-1,0,-1 do
        local pos = {x=x, y=y, z=z}
        if dfhack.maps.isValidTilePos(pos) then
            local info = terrain_info(pos)
            if not info.hidden and info.shape ~= 'EMPTY' then return pos, info end
        end
    end
    return nil, nil
end

local function inventory_index(bounds)
    local result = {}
    local units_scanned = 0
    for _, unit in ipairs(df.global.world.units.active or {}) do
        if units_scanned >= MAX_UNITS_SCANNED then break end
        units_scanned = units_scanned + 1
        local unit_pos = read_position(function() return dfhack.units.getPosition(unit) end)
        if unit_pos ~= nil and unit_pos.x >= bounds.x1 and unit_pos.x <= bounds.x2 and unit_pos.y >= bounds.y1 and unit_pos.y <= bounds.y2 and unit_pos.z == bounds.z then
            local inventory = select(1, safe(function() return unit.inventory end))
            for index, inv_item in ipairs(inventory or {}) do
                local item = select(1, safe(function() return inv_item.item end))
                if item ~= nil and item.id ~= nil then
                    result[item.id] = {unitId=scalar(unit.id), mode=scalar(select(1, safe(function() return inv_item.mode end))), inventoryIndex=index}
                end
            end
        end
    end
    return result
end

local function item_category(item)
    local item_type = enum_name(df.item_type, select(1, safe(function() return item:getType() end)))
    return ({FOOD='food', DRINK='drink', WOOD='wood', BOULDER='stone', CLOTHING='clothing', WEAPON='weapon', TOOL='tool', FURNITURE='furniture', CORPSE='corpse'})[item_type] or 'other'
end

local function item_quantity(item)
    local quantity = scalar(select(1, safe(function() return item.stack_size end)))
    if type(quantity) ~= 'number' or quantity < 1 then return 1 end
    return math.floor(quantity)
end

local function loose_items(bounds)
    local inventory = inventory_index(bounds)
    local items = {}
    local scanned = 0
    for _, item in ipairs(df.global.world.items.other.IN_PLAY or {}) do
        if scanned >= CURRENT_MAX_ITEMS_SCANNED then break end
        scanned = scanned + 1
        local pos = read_position(function() return dfhack.items.getPosition(item) end)
        if pos ~= nil and pos.x >= bounds.x1 and pos.x <= bounds.x2 and pos.y >= bounds.y1 and pos.y <= bounds.y2 and pos.z == bounds.z then
            local container = select(1, safe(function() return dfhack.items.getContainer(item) end))
            if container == nil and inventory[item.id] == nil then
                local cell_key = pos.x .. ',' .. pos.y .. ',' .. pos.z
                local cell = items[cell_key] or {objectCount=0, stackQuantity=0, categories={}}
                local category = item_category(item)
                local category_count = cell.categories[category] or {objectCount=0, stackQuantity=0}
                category_count.objectCount = category_count.objectCount + 1
                category_count.stackQuantity = category_count.stackQuantity + item_quantity(item)
                cell.categories[category] = category_count
                cell.objectCount = cell.objectCount + 1
                cell.stackQuantity = cell.stackQuantity + item_quantity(item)
                items[cell_key] = cell
            end
        end
    end
    return items
end

local function unit_index(bounds, observer_id)
    local result = {}
    local units = dfhack.units.getUnitsInBox(bounds.x1, bounds.y1, bounds.z, bounds.x2, bounds.y2, bounds.z) or {}
    for index, unit in ipairs(units) do
        if index > MAX_UNITS_SCANNED then break end
        local pos = read_position(function() return dfhack.units.getPosition(unit) end)
        if pos ~= nil and pos.z == bounds.z then
            local cell_key = pos.x .. ',' .. pos.y .. ',' .. pos.z
            local entry = result[cell_key] or {citizen=0, other=0, invader=0, dangerous=0}
            if scalar(unit.id) == observer_id then
                entry.observer = true
            else
                entry.citizen = entry.citizen + 1
            end
            result[cell_key] = entry
        end
    end
    return result
end

local function detail_from_cell(dx, dy, info, units, items)
    local has_structure = info.building ~= nil and 'building' or info.construction ~= nil and 'construction' or info.plant ~= nil and 'plant' or nil
    local has_liquid = info.flowSize > 0 and info.flowSize or nil
    local has_units = units ~= nil and (units.citizen + units.other + units.invader + units.dangerous) > 0
    if not has_units and items == nil and has_structure == nil and has_liquid == nil then return nil end
    local summary = nil
    if items ~= nil then
        local categories = {}
        for category, value in pairs(items.categories) do
            table.insert(categories, {category=category, objectCount=value.objectCount, stackQuantity=value.stackQuantity})
        end
        table.sort(categories, function(a, b) return a.category < b.category end)
        summary = {objectCount=items.objectCount, stackQuantity=items.stackQuantity, categories=categories}
    end
    return {dx=dx, dy=dy, citizenCount=units and units.citizen or 0, otherUnitCount=units and units.other or 0, invaderCount=units and units.invader or 0, dangerousUnitCount=units and units.dangerous or 0, items=summary, structureClass=has_structure, liquidDepth=has_liquid}
end

local function current_plane(bounds, center, observer_id, sampled, map_size, warnings)
    local terrain_rows, feature_rows, material_rows, unit_rows = {}, {}, {}, {}
    local unit_lookup = sampled and {} or unit_index(bounds, observer_id)
    local item_lookup = sampled and {} or loose_items(bounds)
    local details = {}
    for row=0,bounds.height-1 do
        local terrain_row, feature_row, material_row, unit_row = {}, {}, {}, {}
        for column=0,bounds.width-1 do
            local x, y = bounds.x1 + column, bounds.y1 + row
            local pos, info
            if sampled then pos, info = surface_position(x, y, map_size) else pos = {x=x, y=y, z=bounds.z}; if dfhack.maps.isValidTilePos(pos) then info = terrain_info(pos) end end
            local dx, dy = x-center.x, y-center.y
            if info == nil or info.hidden then
                table.insert(terrain_row, '?'); table.insert(feature_row, ' '); table.insert(material_row, '?'); table.insert(unit_row, ' ')
            else
                table.insert(terrain_row, terrain_symbol(info)); table.insert(feature_row, feature_symbol(info)); table.insert(material_row, material_symbol(info.material))
                local unit_entry = unit_lookup[x .. ',' .. y .. ',' .. center.z]
                local observer_cell = x == center.x and y == center.y
                table.insert(unit_row, observer_cell and '@' or unit_entry and unit_entry.citizen > 0 and 'd' or ' ')
                if not sampled and math.abs(dx) <= 16 and math.abs(dy) <= 16 then
                    local detail = detail_from_cell(dx, dy, info, unit_entry, item_lookup[x .. ',' .. y .. ',' .. center.z])
                    if detail ~= nil then append_limited(details, detail, CURRENT_MAX_DETAILS) end
                end
            end
        end
        table.insert(terrain_rows, table.concat(terrain_row)); table.insert(feature_rows, table.concat(feature_row)); table.insert(material_rows, table.concat(material_row)); table.insert(unit_rows, table.concat(unit_row))
    end
    return {terrainRows=terrain_rows, featureRows=feature_rows, materialRows=material_rows, unitRows=unit_rows}, details
end

local function current_scene(unit_id)
    local unit = find_unit(unit_id)
    if unit == nil then return failure('INVALID_ARGUMENT', 'No unit exists with the requested unitId.') end
    local center = read_position(function() return dfhack.units.getPosition(unit) end)
    if center == nil then return failure('INVALID_ARGUMENT', 'The requested unit has no valid map position.') end
    local map_x, map_y, map_z = dfhack.maps.getTileSize()
    local map_size = {x=map_x, y=map_y, z=map_z}
    local observer_info = terrain_info(center)
    if observer_info.hidden then return failure('INVALID_DATA', 'The requested observer tile is hidden.') end
    local local_bounds = current_bounds(center, CURRENT_LOCAL_SIZE, CURRENT_LOCAL_SIZE)
    local site_bounds = current_bounds(center, CURRENT_SITE_WIDTH, CURRENT_SITE_HEIGHT)
    local warnings = {}
    local site_plane = current_plane(site_bounds, center, scalar(unit.id), true, map_size, warnings)
    local local_plane, details = current_plane(local_bounds, center, scalar(unit.id), false, map_size, warnings)
    local flags = {outside=observer_info.outside, light=observer_info.light, subterranean=observer_info.subterranean, hidden=observer_info.hidden}
    local structure_class = observer_info.building ~= nil and 'building' or observer_info.construction ~= nil and 'construction' or nil
    emit({
        schemaVersion=CURRENT_SCENE_SCHEMA_VERSION,
        provenance={kind='live-dfhack', generatedBy='fortress-souls/get-dwarf-surroundings'},
        gameTime={year=dfhack.world.ReadCurrentYear(), tick=dfhack.world.ReadCurrentTick()},
        observer={environment=environment_label(flags), flags=flags, terrain={shape=string.lower(observer_info.shape or 'unknown'), material=string.lower(observer_info.material or 'unknown')}, structureClass=structure_class},
        siteOverview={projection='surface_overview', width=CURRENT_SITE_WIDTH, height=CURRENT_SITE_HEIGHT, sampled=true, terrainRows=site_plane.terrainRows, featureRows=site_plane.featureRows, materialRows=site_plane.materialRows, unitRows=site_plane.unitRows},
        localMap={projection='current_level', width=CURRENT_LOCAL_SIZE, height=CURRENT_LOCAL_SIZE, sampled=false, terrainRows=local_plane.terrainRows, featureRows=local_plane.featureRows, materialRows=local_plane.materialRows, unitRows=local_plane.unitRows},
        details=details,
        warnings=warnings
    })
end

local function sorted_legend(values)
    local result = {}
    for value in pairs(values) do table.insert(result, value) end
    table.sort(result)
    return result
end

local function main(...)
    local args = {...}
    if not dfhack.isMapLoaded() then
        return failure('NO_MAP_LOADED', 'DFHack is reachable, but no fortress map is loaded.')
    end

    local unit_id, unit_err = integer(args[1], 'unitId', 0, 2147483647)
    if unit_err then return failure('INVALID_ARGUMENT', unit_err) end

    if args[2] == nil then
        return current_scene(unit_id)
    end

    local radius = DEFAULT_RADIUS
    if args[2] ~= nil then
        local parsed_radius, radius_err = integer(args[2], 'radius', DEFAULT_RADIUS, MAX_RADIUS)
        if radius_err then return failure('INVALID_ARGUMENT', radius_err) end
        radius = parsed_radius
    end

    local unit = find_unit(unit_id)
    if unit == nil then
        return failure('INVALID_ARGUMENT', 'No unit exists with the requested unitId.')
    end

    local center = read_position(function() return dfhack.units.getPosition(unit) end)
    if center == nil then
        return failure('INVALID_ARGUMENT', 'The requested unit has no valid map position.')
    end

    local bounds, bounds_err = validate_bounds(center, radius)
    if bounds_err then return failure('INVALID_BOUNDS', bounds_err) end

    local warnings = {}
    local legend_values = {}
    local unit_counts = index_unit_counts(bounds, warnings)
    local cells = {}

    for dy=-radius,radius do
        for dx=-radius,radius do
            local pos = {x=center.x+dx, y=center.y+dy, z=center.z}
            local tiletype = dfhack.maps.getTileType(pos)
            local attrs = tiletype and df.tiletype.attrs[tiletype] or nil
            local flags = select(1, dfhack.maps.getTileFlags(pos))
            local is_hidden = flags ~= nil and flags.hidden == true
            local cell = {dx=dx, dy=dy, visibility=is_hidden and 'hidden' or 'visible'}

            if not is_hidden then
                local building = dfhack.buildings.findAtTile(pos)
                local zones = dfhack.buildings.findCivzonesAt(pos) or {}
                local has_building = building ~= nil or #zones > 0
                local cell_key = key(pos)
                local terrain = terrain_class(attrs, has_building)
                local shape_attrs = attrs and df.tiletype_shape.attrs[attrs.shape] or nil
                local walkable = shape_attrs and walkable_flag(shape_attrs.walkable) or nil

                if terrain ~= nil then
                    cell.terrainClass = terrain
                    legend_values[terrain] = true
                end

                if walkable ~= nil then
                    cell.walkable = walkable
                end

                if has_building then
                    cell.featureClass = 'building'
                    legend_values['building'] = true
                end

                local unit_count = unit_counts[cell_key] or 0
                if unit_count > 0 then
                    cell.unitCount = unit_count
                end
            end

            table.insert(cells, cell)
        end
    end

    emit({
        schemaVersion=SCHEMA_VERSION,
        provenance={kind='live-dfhack', generatedBy='fortress-souls/get-dwarf-surroundings'},
        gameTime={year=dfhack.world.ReadCurrentYear(), tick=dfhack.world.ReadCurrentTick()},
        bounds={radius=radius, width=bounds.width, height=bounds.height},
        cells=cells,
        legend=sorted_legend(legend_values),
        warnings=warnings
    })
end

local script_args = {...}
local _, err = safe(function() main(table.unpack(script_args)) end)
if err then failure('SCRIPT_FAILED', err) end
