# Voxel prototype in Unity

![](https://github.com/d-bucur/demos/raw/master/mycraft1.gif)


## Description
My attempt at recreating a basic version of an 'infinite' procedurally generated voxel world.

## Controls
- **Left click** to build
- **Right** click to destroy
- **WASD** to move
- **Space** to jump
- Hold **shift** for speed and jump boost

## Details
You can configure most of the details of the world inside the *WorldBuilder* object, like how big each sector is and how far they are rendered. The world is generated by overlapping a bunch of configurable Perlin noise maps.

For each sector only the visible faces of cubes are generated (the ones facing empty blocks that you can walk in). New sectors are generated during multiple frames trying to maintain a 60 FPS target. The generation is divided into 2 phases that alternate between frames

### 1.Generate mesh data

Launches in parallel jobs for each sector:

**SectorGenerationJob**: Fills each block with a type by sampling noise maps
and overriding values with player modifications

**MeshGenerationJob**: Sweeps the generated blocks to generate data for visible
faces

![](https://github.com/d-bucur/demos/raw/master/mycraft/profiler1.png)

### 2.Create Unity meshes

Given data from the jobs in the previous phase, this creates a collision mesh for the terrain, and launches a job to [bake it](https://docs.unity3d.com/2019.3/Documentation/ScriptReference/Physics.BakeMesh.html) for usage with a mesh collider

While the jobs finish, render meshes are created and assigned

![](https://github.com/d-bucur/demos/raw/master/mycraft/profiler2.png)

