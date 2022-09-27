﻿module MaxHelpingHandFlagDecal

using ..Ahorn, Maple

@mapdef Entity "MaxHelpingHand/FlagDecal" FlagDecal(x::Integer, y::Integer, fps::Number=12.0, flag::String="decal_flag", inverted::Bool=false,
    decalPath::String="1-forsakencity/flag", appearAnimationPath::String="", disappearAnimationPath::String="", depth::Int=8999)

const placements = Ahorn.PlacementDict(
    "Flag Decal (max480's Helping Hand)" => Ahorn.EntityPlacement(
        FlagDecal
    )
)

Ahorn.editingOptions(entity::FlagDecal) = Dict{String, Any}(
    "depth" => Dict{String, Int}(
        "In front of FG" => -10501,
        "Behind FG" => -10499,
        "In front of BG" => 8999,
        "Behind BG" => 9001
    )
)

function getSpritePath(entity::FlagDecal)
    sprite = Ahorn.getSprite("decals/" * entity.decalPath * "00", "Gameplay")
    if sprite.surface !== Ahorn.Assets.missingImage
        return "decals/" * entity.decalPath * "00"
    else
        return "decals/" * entity.decalPath
    end
end

function Ahorn.selection(entity::FlagDecal)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(getSpritePath(entity), x, y)
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::FlagDecal, room::Maple.Room)
    Ahorn.drawSprite(ctx, getSpritePath(entity), 0, 0)
end

end