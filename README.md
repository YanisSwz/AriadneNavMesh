# <img src="./Images/spool.png" width="35" height="35"> AriadneNavMesh
This is a personal project aiming to implement the first steps of navmesh generation (geometry input, triangle voxelization and walkable filtering) in Unity. My main reference is [Recast Navigation](https://github.com/recastnavigation/recastnavigation).


## Geometry input

Started with a GeometryGetter class to see how I could retrieve scene geometry in Unity. I used the mesh filter component, so for now it takes only render meshes into account (might add option for physics colliders later).

![Geometry Getter](./Screens/Debug.png?raw=true)

## Triangle voxelization

Sutherland-Hodgman polygon clipping algorithm: https://www.youtube.com/watch?v=Euuw72Ymu0M
