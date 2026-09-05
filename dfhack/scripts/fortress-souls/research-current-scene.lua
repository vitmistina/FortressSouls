local json = require('json')

local SCHEMA_VERSION = 'fortress-souls-current-scene-research.v0.1'
local LOCAL_RADIUS = 16
local SITE_WIDTH = 24
local SITE_HEIGHT = 12
local MAX_ITEMS_SCANNED = 200000
local MAX_UNITS_SCANNED = 10000
local MAX_SURFACE_COLUMNS = 16
local MAX_EXAMPLES = 24

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

local function enum_name(enum, value)
    if enum == nil or value == nil then return nil end
    return scalar(select(1, safe(function() return enum[value] end)))
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

local function in_bounds(pos, bounds)
    return pos ~= nil and pos.x >= bounds.x1 and pos.x <= bounds.x2 and
        pos.y >= bounds.y1 and pos.y <= bounds.y2 and pos.z == bounds.z
end

local function append_limited(list, value, limit)
    if #list < limit then table.insert(list, value) end
end

local function sorted_keys(values)
    local result = {}
    for value in pairs(values) do table.insert(result, value) end
    table.sort(result)
    return result
end

local function terrain_info(pos)
    local tiletype = select(1, safe(function() return dfhack.maps.getTileType(pos) end))
    local attrs = tiletype and df.tiletype.attrs[tiletype] or nil
    local shape = attrs and enum_name(df.tiletype_shape, attrs.shape) or nil
    local material = attrs and enum_name(df.tiletype_material, attrs.material) or nil
    local special = attrs and enum_name(df.tiletype_special, attrs.special) or nil
    local variant = attrs and enum_name(df.tiletype_variant, attrs.variant) or nil
    local flags = select(1, safe(function() return dfhack.maps.getTileFlags(pos) end))
    local flow_size = flags and scalar(flags.flow_size) or 0
    local liquid_type = nil
    if flow_size > 0 then liquid_type = flags.liquid_type and 'MAGMA' or 'WATER' end
    local building = select(1, safe(function() return dfhack.buildings.findAtTile(pos) end))
    local plant = select(1, safe(function() return dfhack.maps.getPlantAtTile(pos) end))
    local construction = select(1, safe(function() return dfhack.constructions.findAtTile(pos) end))
    return {
        tiletype=enum_name(df.tiletype, tiletype), shape=shape, material=material,
        special=special, variant=variant, hidden=flags ~= nil and flags.hidden == true,
        outside=flags ~= nil and flags.outside == true,
        light=flags ~= nil and flags.light == true,
        subterranean=flags ~= nil and flags.subterranean == true,
        flowSize=flow_size, liquidType=liquid_type,
        building=building, plant=plant, construction=construction
    }
end

local function visible_symbol(info)
    if info.hidden then return '?' end
    if info.liquidType == 'MAGMA' then return 'M' end
    if info.liquidType == 'WATER' then return '~' end
    if info.building ~= nil then return 'B' end
    if info.plant ~= nil then return 'p' end
    if info.construction ~= nil then return 'C' end
    if info.shape == 'WALL' or info.shape == 'FORTIFICATION' then return '#' end
    if info.shape == 'RAMP' or info.shape == 'RAMP_TOP' then return '^' end
    if info.shape and string.find(info.shape, 'STAIR', 1, true) then return '>' end
    if info.shape == 'FLOOR' then return '.' end
    if info.shape == 'EMPTY' or info.shape == 'BROOK_TOP' then return ' ' end
    return '?'
end

local function classify(flags)
    if flags == nil then
        return {label='unknown', rule='tile flags unavailable', warnings={'ENVIRONMENT_FLAGS_UNAVAILABLE'}}
    end
    local outside = flags.outside == true
    local light = flags.light == true
    local subterranean = flags.subterranean == true
    if subterranean then
        return {label='underground', rule='subterranean=true', warnings={}}
    end
    if outside and light then
        return {label='outdoor', rule='outside=true and light=true and subterranean=false', warnings={}}
    end
    if not outside and light then
        return {label='sheltered', rule='outside=false and light=true and subterranean=false', warnings={}}
    end
    return {label='unknown', rule='no accepted flag combination', warnings={'ENVIRONMENT_FLAGS_UNCLASSIFIED'}}
end

local function flag_summary(pos)
    local flags = select(1, safe(function() return dfhack.maps.getTileFlags(pos) end))
    local result = {
        source='dfhack.maps.getTileFlags',
        outside=flags ~= nil and flags.outside == true,
        light=flags ~= nil and flags.light == true,
        subterranean=flags ~= nil and flags.subterranean == true,
        hidden=flags ~= nil and flags.hidden == true
    }
    local classification = classify(flags)
    result.classification = classification.label
    result.rule = classification.rule
    result.warnings = classification.warnings
    return result
end

local function observer_from_unit(unit_id)
    local unit = select(1, safe(function() return df.unit.find(unit_id) end))
    if unit == nil then return nil, 'No unit exists with the requested unitId.' end
    local pos = read_position(function() return dfhack.units.getPosition(unit) end)
    if pos == nil then return nil, 'The requested unit has no valid map position.' end
    return {
        id=scalar(unit.id),
        name=scalar(select(1, safe(function() return dfhack.units.getReadableName(unit) end))),
        position=pos,
        flags=flag_summary(pos)
    }, nil
end

local function point_observer(x, y, z)
    local pos = {x=x, y=y, z=z}
    if not dfhack.maps.isValidTilePos(pos) then return nil, 'The requested point is not a valid map tile.' end
    return {position=pos, flags=flag_summary(pos)}, nil
end

local function scan_unit_environments()
    local result = {counts={outdoor=0, sheltered=0, underground=0, unknown=0}, units={}}
    for _, unit in ipairs(df.global.world.units.active or {}) do
        local pos = read_position(function() return dfhack.units.getPosition(unit) end)
        if pos ~= nil then
            local flags = flag_summary(pos)
            result.counts[flags.classification] = (result.counts[flags.classification] or 0) + 1
            append_limited(result.units, {
                id=scalar(unit.id),
                name=scalar(select(1, safe(function() return dfhack.units.getReadableName(unit) end))),
                position=pos,
                flags=flags
            }, MAX_UNITS_SCANNED)
        end
    end
    return result
end

local function find_sheltered_tiles(map_size)
    local result = {sampleStride=4, sampled=0, counts={outdoor=0, sheltered=0, underground=0, unknown=0}, examples={}, shelteredExamples={}}
    local seen_z = {}
    for z=0,map_size.z-1,4 do seen_z[z] = true end
    for _, unit in ipairs(df.global.world.units.active or {}) do
        local pos = read_position(function() return dfhack.units.getPosition(unit) end)
        if pos ~= nil then seen_z[pos.z] = true end
    end
    local z_values = {}
    for z in pairs(seen_z) do table.insert(z_values, z) end
    table.sort(z_values)
    for _, z in ipairs(z_values) do
        for y=0,map_size.y-1,4 do
            for x=0,map_size.x-1,4 do
                if result.sampled >= 200000 then return result end
                result.sampled = result.sampled + 1
                local pos = {x=x, y=y, z=z}
                if dfhack.maps.isValidTilePos(pos) then
                    local flags = flag_summary(pos)
                    result.counts[flags.classification] = (result.counts[flags.classification] or 0) + 1
                    append_limited(result.examples, {position=pos, flags=flags}, MAX_EXAMPLES)
                    if flags.classification == 'sheltered' then append_limited(result.shelteredExamples, {position=pos, flags=flags}, MAX_EXAMPLES) end
                end
            end
        end
    end
    return result
end

local function find_features(map_size)
    local result = {
        sampleStride=4, zStride=2, sampled=0,
        counts={water=0, magma=0, plants=0, ramps=0, walls=0, stairs=0, buildings=0},
        examples={water={}, magma={}, plants={}, ramps={}, walls={}, stairs={}, buildings={}}
    }
    local seen_z = {}
    for z=0,map_size.z-1,2 do seen_z[z] = true end
    for _, unit in ipairs(df.global.world.units.active or {}) do
        local pos = read_position(function() return dfhack.units.getPosition(unit) end)
        if pos ~= nil then seen_z[pos.z] = true end
    end
    local z_values = {}
    for z in pairs(seen_z) do table.insert(z_values, z) end
    table.sort(z_values)
    local function add_feature(kind, pos, info)
        result.counts[kind] = result.counts[kind] + 1
        append_limited(result.examples[kind], {position=pos, terrain={shape=info.shape, material=info.material, tiletype=info.tiletype}, hidden=info.hidden, outside=info.outside}, MAX_EXAMPLES)
    end
    for _, z in ipairs(z_values) do
        local x_step = z == 173 and 1 or 4
        local y_step = z == 173 and 1 or 4
        for y=0,map_size.y-1,y_step do
            for x=0,map_size.x-1,x_step do
                if result.sampled >= 300000 then return result end
                result.sampled = result.sampled + 1
                local pos = {x=x, y=y, z=z}
                if dfhack.maps.isValidTilePos(pos) then
                    local info = terrain_info(pos)
                    if info.liquidType == 'WATER' then add_feature('water', pos, info) end
                    if info.liquidType == 'MAGMA' then add_feature('magma', pos, info) end
                    if info.plant ~= nil then add_feature('plants', pos, info) end
                    if info.shape == 'RAMP' or info.shape == 'RAMP_TOP' then add_feature('ramps', pos, info) end
                    if info.shape == 'WALL' or info.shape == 'FORTIFICATION' then add_feature('walls', pos, info) end
                    if info.shape and string.find(info.shape, 'STAIR', 1, true) then add_feature('stairs', pos, info) end
                    if info.building ~= nil then add_feature('buildings', pos, info) end
                end
            end
        end
    end
    return result
end

local function find_first_stair(map_size)
    local result = {scanned=0, found=false, examples={}}
    for z=0,map_size.z-1 do
        for y=0,map_size.y-1 do
            for x=0,map_size.x-1 do
                result.scanned = result.scanned + 1
                local pos = {x=x, y=y, z=z}
                local tiletype = select(1, safe(function() return dfhack.maps.getTileType(pos) end))
                local attrs = tiletype and df.tiletype.attrs[tiletype] or nil
                local shape = attrs and enum_name(df.tiletype_shape, attrs.shape) or nil
                if shape and string.find(shape, 'STAIR', 1, true) then
                    local flags = select(1, safe(function() return dfhack.maps.getTileFlags(pos) end))
                    table.insert(result.examples, {
                        position=pos,
                        terrain={shape=shape, tiletype=enum_name(df.tiletype, tiletype), material=attrs and enum_name(df.tiletype_material, attrs.material) or nil},
                        hidden=flags ~= nil and flags.hidden == true,
                        outside=flags ~= nil and flags.outside == true,
                        light=flags ~= nil and flags.light == true,
                        subterranean=flags ~= nil and flags.subterranean == true
                    })
                    result.found = true
                    return result
                end
            end
        end
    end
    return result
end

local function bounds_for(center, width, height)
    return {
        x1=center.x-math.floor(width/2), y1=center.y-math.floor(height/2), z=center.z,
        x2=center.x-math.floor(width/2)+width-1, y2=center.y-math.floor(height/2)+height-1,
        width=width, height=height
    }
end

local function collect_plane(bounds)
    local rows = {}
    local counts = {valid=0, outsideMap=0, hidden=0, water=0, magma=0, buildings=0, plants=0, constructions=0, walls=0, ramps=0, stairs=0}
    local examples = {}
    local hidden_cells = {}
    for y=bounds.y1,bounds.y2 do
        local row = {}
        for x=bounds.x1,bounds.x2 do
            local pos = {x=x, y=y, z=bounds.z}
            if not dfhack.maps.isValidTilePos(pos) then
                counts.outsideMap = counts.outsideMap + 1
                table.insert(row, ' ')
            else
                counts.valid = counts.valid + 1
                local info = terrain_info(pos)
                if info.hidden then
                    counts.hidden = counts.hidden + 1
                    table.insert(row, '?')
                    append_limited(hidden_cells, {dx=x-(bounds.x1+math.floor((bounds.width-1)/2)), dy=y-(bounds.y1+math.floor((bounds.height-1)/2)), visibility='hidden'}, MAX_EXAMPLES)
                else
                    if info.liquidType == 'WATER' then counts.water = counts.water + 1 end
                    if info.liquidType == 'MAGMA' then counts.magma = counts.magma + 1 end
                    if info.building ~= nil then counts.buildings = counts.buildings + 1 end
                    if info.plant ~= nil then counts.plants = counts.plants + 1 end
                    if info.construction ~= nil then counts.constructions = counts.constructions + 1 end
                    if info.shape == 'WALL' or info.shape == 'FORTIFICATION' then counts.walls = counts.walls + 1 end
                    if info.shape == 'RAMP' or info.shape == 'RAMP_TOP' then counts.ramps = counts.ramps + 1 end
                    if info.shape and string.find(info.shape, 'STAIR', 1, true) then counts.stairs = counts.stairs + 1 end
                    table.insert(row, visible_symbol(info))
                    if info.liquidType ~= nil or info.building ~= nil or info.plant ~= nil or info.construction ~= nil or info.shape == 'WALL' or info.shape == 'RAMP' or (info.shape and string.find(info.shape, 'STAIR', 1, true)) then
                        append_limited(examples, {dx=x-(bounds.x1+math.floor((bounds.width-1)/2)), dy=y-(bounds.y1+math.floor((bounds.height-1)/2)), terrain={shape=info.shape, material=info.material, tiletype=info.tiletype}, liquid={type=info.liquidType, depth=info.flowSize}, feature=info.building and 'building' or info.plant and 'plant' or info.construction and 'construction' or nil}, MAX_EXAMPLES)
                    end
                end
            end
        end
        table.insert(rows, table.concat(row))
    end
    return {bounds=bounds, grid=rows, counts=counts, hiddenCellSamples=hidden_cells, hiddenCellRule='hidden cells retain only dx, dy, and visibility=hidden; no terrain, item, unit, material, or feature fields are emitted', visibleFeatureExamples=examples}
end

local function inventory_index(bounds)
    local result = {}
    local units_scanned = 0
    local units_with_inventory = 0
    for _, unit in ipairs(df.global.world.units.active or {}) do
        if units_scanned >= MAX_UNITS_SCANNED then break end
        units_scanned = units_scanned + 1
        local unit_pos = read_position(function() return dfhack.units.getPosition(unit) end)
        if in_bounds(unit_pos, bounds) then
            local inventory = select(1, safe(function() return unit.inventory end))
            if inventory ~= nil then
                units_with_inventory = units_with_inventory + 1
                for index, inv_item in ipairs(inventory) do
                    local item = select(1, safe(function() return inv_item.item end))
                    if item ~= nil and item.id ~= nil then
                        local mode = select(1, safe(function() return inv_item.mode end))
                        result[item.id] = {
                            unitId=scalar(unit.id), mode=scalar(mode),
                            modeName=enum_name(df.inv_item_role_type, mode),
                            bodyPartId=scalar(select(1, safe(function() return inv_item.body_part_id end))),
                            inventoryIndex=index
                        }
                    end
                end
            end
        end
    end
    return result, {unitsScanned=units_scanned, unitsWithInventory=units_with_inventory}
end

local function scan_inventory_modes()
    local counts = {}
    local examples = {}
    local non_worn_examples = {}
    local units_scanned = 0
    local inventory_entries = 0
    for _, unit in ipairs(df.global.world.units.active or {}) do
        if units_scanned >= MAX_UNITS_SCANNED then break end
        units_scanned = units_scanned + 1
        local inventory = select(1, safe(function() return unit.inventory end))
        if inventory ~= nil then
            for index, inv_item in ipairs(inventory) do
                local item = select(1, safe(function() return inv_item.item end))
                if item ~= nil and item.id ~= nil then
                    inventory_entries = inventory_entries + 1
                    local mode = select(1, safe(function() return inv_item.mode end))
                    local mode_name = enum_name(df.inv_item_role_type, mode)
                    counts[mode_name] = (counts[mode_name] or 0) + 1
                    local example = {
                        unitId=scalar(unit.id),
                        itemId=scalar(item.id),
                        inventoryIndex=index,
                        mode=scalar(mode),
                        modeName=mode_name,
                        description=scalar(select(1, safe(function() return dfhack.items.getReadableDescription(item) end)))
                    }
                    append_limited(examples, example, MAX_EXAMPLES)
                    if mode_name ~= 'Worn' then append_limited(non_worn_examples, example, MAX_EXAMPLES) end
                end
            end
        end
    end
    return {unitsScanned=units_scanned, inventoryEntries=inventory_entries, modeCounts=counts, examples=examples, nonWornExamples=non_worn_examples}
end

local function collect_items(bounds)
    local inventory, inventory_scan = inventory_index(bounds)
    local counts = {scanned=0, inBounds=0, contained=0, inventoryReferenced=0, looseCandidates=0}
    local examples = {}
    local inventory_examples = {}
    for _, item in ipairs(df.global.world.items.other.IN_PLAY or {}) do
        if counts.scanned >= MAX_ITEMS_SCANNED then break end
        counts.scanned = counts.scanned + 1
        local pos = read_position(function() return dfhack.items.getPosition(item) end)
        if in_bounds(pos, bounds) then
            counts.inBounds = counts.inBounds + 1
            local container = select(1, safe(function() return dfhack.items.getContainer(item) end))
            local reference = inventory[item.id]
            if container ~= nil then counts.contained = counts.contained + 1 end
            if reference ~= nil then counts.inventoryReferenced = counts.inventoryReferenced + 1 end
            if container == nil and reference == nil then counts.looseCandidates = counts.looseCandidates + 1 end
            local example = {
                    id=scalar(item.id), position=pos,
                    type=enum_name(df.item_type, select(1, safe(function() return item:getType() end))),
                    description=scalar(select(1, safe(function() return dfhack.items.getReadableDescription(item) end))),
                    contained=container ~= nil, containerId=container and scalar(container.id) or nil,
                    inventory=reference
                }
            if reference ~= nil then append_limited(inventory_examples, example, MAX_EXAMPLES) end
            if container ~= nil or #examples < MAX_EXAMPLES then append_limited(examples, example, MAX_EXAMPLES) end
        end
    end
    return {scan=inventory_scan, counts=counts, examples=examples, inventoryExamples=inventory_examples, exclusionRule='exclude item when dfhack.items.getContainer(item) is non-nil or item.id is referenced by a nearby unit.inventory entry; retain only remaining loose candidates'}, nil
end

local function collect_buildings(bounds)
    local by_id = {}
    for y=bounds.y1,bounds.y2 do
        for x=bounds.x1,bounds.x2 do
            local pos = {x=x, y=y, z=bounds.z}
            if dfhack.maps.isValidTilePos(pos) then
                local building = select(1, safe(function() return dfhack.buildings.findAtTile(pos) end))
                if building ~= nil then
                    local id = scalar(building.id) or ('tile-' .. x .. '-' .. y)
                    if by_id[id] == nil then
                        local building_type = scalar(select(1, safe(function() return building:getType() end)))
                        by_id[id] = {
                            id=scalar(building.id), type=building_type,
                            typeName=enum_name(df.building_type, building_type),
                            firstTile={x=x, y=y, z=bounds.z},
                            subtype=scalar(select(1, safe(function() return building:getSubtype() end)))
                        }
                    end
                end
            end
        end
    end
    local result = {}
    for _, building in pairs(by_id) do table.insert(result, building) end
    table.sort(result, function(a, b) return tostring(a.typeName) < tostring(b.typeName) end)
    return {buildings=result, wagonCandidates=(function()
        local candidates = {}
        for _, building in ipairs(result) do
            if building.typeName == 'WAGON' or building.typeName == 'WAGON_WHEEL' or building.typeName == 'CART' or building.type == 32 then
                table.insert(candidates, building)
            end
        end
        return candidates
    end)()}
end

local function scan_surface_columns(center, map_size)
    local columns = {}
    local start_x = math.max(0, center.x-6)
    local start_y = math.max(0, center.y-6)
    for y=start_y,math.min(map_size.y-1, start_y+3) do
        for x=start_x,math.min(map_size.x-1, start_x+3) do
            if #columns >= MAX_SURFACE_COLUMNS then break end
            local candidates = {topmostValid=nil, firstRevealed=nil, firstOutside=nil, firstRevealedOutside=nil, firstNonOpenRevealed=nil}
            for z=map_size.z-1,0,-1 do
                local pos = {x=x, y=y, z=z}
                if dfhack.maps.isValidTilePos(pos) then
                    local info = terrain_info(pos)
                    local observation = {z=z, shape=info.shape, tiletype=info.tiletype, hidden=info.hidden, outside=info.outside, light=info.light, subterranean=info.subterranean}
                    if candidates.topmostValid == nil then candidates.topmostValid=observation end
                    if not info.hidden and candidates.firstRevealed == nil then candidates.firstRevealed=observation end
                    if info.outside and candidates.firstOutside == nil then candidates.firstOutside=observation end
                    if not info.hidden and info.outside and candidates.firstRevealedOutside == nil then candidates.firstRevealedOutside=observation end
                    if not info.hidden and info.shape ~= 'EMPTY' and candidates.firstNonOpenRevealed == nil then candidates.firstNonOpenRevealed=observation end
                    if candidates.firstRevealed ~= nil and candidates.firstOutside ~= nil and candidates.firstRevealedOutside ~= nil and candidates.firstNonOpenRevealed ~= nil then break end
                end
            end
            table.insert(columns, {x=x, y=y, candidates=candidates})
        end
        if #columns >= MAX_SURFACE_COLUMNS then break end
    end
    return {algorithmNotes={
        'topmostValid: first valid tile scanning from map top downward',
        'firstRevealed: first tile with hidden=false scanning from map top downward',
        'firstOutside: first tile with outside=true scanning from map top downward',
        'firstRevealedOutside: first tile satisfying hidden=false and outside=true',
        'firstNonOpenRevealed: first revealed tile whose terrain shape is not EMPTY'
    }, columns=columns}
end

local function emit(value)
    local encoded = json.encode(value, {pretty=false})
    print(encoded)
end

local function failure(code, message)
    emit({schemaVersion=SCHEMA_VERSION, error={code=code, message=message}, provenance={kind='live-dfhack', generatedBy='fortress-souls/research-current-scene'}})
end

local function main(...)
    local args = {...}
    if not dfhack.isWorldLoaded() or not dfhack.isMapLoaded() then
        return failure('NO_MAP_LOADED', 'A loaded world and map are required.')
    end
    local map_x, map_y, map_z = dfhack.maps.getTileSize()
    local map_size = {x=map_x, y=map_y, z=map_z}
    local observer, observer_error
    if args[1] == 'scan-units' then
        emit({
            schemaVersion=SCHEMA_VERSION,
            provenance={kind='live-dfhack', generatedBy='fortress-souls/research-current-scene'},
            gameTime={year=dfhack.world.ReadCurrentYear(), tick=dfhack.world.ReadCurrentTick()},
            mapSize=map_size,
            unitEnvironmentScan=scan_unit_environments(),
            safety={writesFiles=false, invokesCommands=false}
        })
        return
    elseif args[1] == 'find-sheltered' then
        emit({
            schemaVersion=SCHEMA_VERSION,
            provenance={kind='live-dfhack', generatedBy='fortress-souls/research-current-scene'},
            gameTime={year=dfhack.world.ReadCurrentYear(), tick=dfhack.world.ReadCurrentTick()},
            mapSize=map_size,
            shelteredTileScan=find_sheltered_tiles(map_size),
            safety={writesFiles=false, invokesCommands=false}
        })
        return
    elseif args[1] == 'find-features' then
        emit({
            schemaVersion=SCHEMA_VERSION,
            provenance={kind='live-dfhack', generatedBy='fortress-souls/research-current-scene'},
            gameTime={year=dfhack.world.ReadCurrentYear(), tick=dfhack.world.ReadCurrentTick()},
            mapSize=map_size,
            featureScan=find_features(map_size),
            safety={writesFiles=false, invokesCommands=false}
        })
        return
    elseif args[1] == 'find-stairs' then
        emit({
            schemaVersion=SCHEMA_VERSION,
            provenance={kind='live-dfhack', generatedBy='fortress-souls/research-current-scene'},
            gameTime={year=dfhack.world.ReadCurrentYear(), tick=dfhack.world.ReadCurrentTick()},
            mapSize=map_size,
            stairScan=find_first_stair(map_size),
            safety={writesFiles=false, invokesCommands=false}
        })
        return
    elseif args[1] == 'scan-inventory-modes' then
        emit({
            schemaVersion=SCHEMA_VERSION,
            provenance={kind='live-dfhack', generatedBy='fortress-souls/research-current-scene'},
            gameTime={year=dfhack.world.ReadCurrentYear(), tick=dfhack.world.ReadCurrentTick()},
            mapSize=map_size,
            inventoryModeScan=scan_inventory_modes(),
            safety={writesFiles=false, invokesCommands=false}
        })
        return
    elseif args[1] == 'unit' then
        local unit_id = tonumber(args[2])
        if unit_id == nil or unit_id ~= math.floor(unit_id) then return failure('INVALID_ARGUMENT', 'Expected unit <integer>.') end
        observer, observer_error = observer_from_unit(unit_id)
    elseif args[1] == 'point' then
        local x, y, z = tonumber(args[2]), tonumber(args[3]), tonumber(args[4])
        if x == nil or y == nil or z == nil or x ~= math.floor(x) or y ~= math.floor(y) or z ~= math.floor(z) then return failure('INVALID_ARGUMENT', 'Expected point <x> <y> <z>.') end
        observer, observer_error = point_observer(x, y, z)
    else
        return failure('INVALID_ARGUMENT', 'Expected scan-units, find-sheltered, find-features, find-stairs, scan-inventory-modes, unit <unitId>, or point <x> <y> <z>.')
    end
    if observer == nil then return failure('INVALID_ARGUMENT', observer_error or 'Unable to resolve observer.') end

    local local_bounds = bounds_for(observer.position, 33, 33)
    local site_bounds = bounds_for(observer.position, SITE_WIDTH, SITE_HEIGHT)
    local local_plane = collect_plane(local_bounds)
    local site_plane = collect_plane(site_bounds)
    local items = collect_items(local_bounds)
    local buildings = collect_buildings(local_bounds)
    local surface = scan_surface_columns(observer.position, map_size)

    emit({
        schemaVersion=SCHEMA_VERSION,
        provenance={kind='live-dfhack', generatedBy='fortress-souls/research-current-scene'},
        gameTime={year=dfhack.world.ReadCurrentYear(), tick=dfhack.world.ReadCurrentTick()},
        mapSize=map_size,
        query={mode=args[1], unitId=args[1] == 'unit' and tonumber(args[2]) or nil},
        observer=observer,
        localPlane=local_plane,
        sitePlane=site_plane,
        inventoryEvidence=items,
        buildingEvidence=buildings,
        surfaceCandidates=surface,
        safety={writesFiles=false, invokesCommands=false, changesUi=false, boundedLocalCells=1089, boundedSiteCells=288, boundedSurfaceColumns=#surface.columns}
    })
end

local script_args = {...}
local _, err = safe(function() main(table.unpack(script_args)) end)
if err then failure('SCRIPT_FAILED', err) end
