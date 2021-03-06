﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class World 
{
    public WorldSettings worldSettings;

    public ChunkData[,,] chunks;
    public List<ChunkCoordinates> activeChunks = new List<ChunkCoordinates>();
    public List<ChunkCoordinates> chunksToCreate = new List<ChunkCoordinates>();
    public List<ChunkData> chunksToUpdate = new List<ChunkData>();

    private Vector3 spawnPosition;
    private ChunkCoordinates lastPlayerChunkCoordinates;
    public ChunkCoordinates currentPlayerChunkCoordinates = new ChunkCoordinates(0, 0, 0);

    private Transform root;
    public bool isCreatingChunks;
    public bool isUpdatingChunks;

    public Vector3 WorldSizeInVoxels
    {
        get
        {
            return new Vector3(worldSettings.worldSizeInChunks.x * worldSettings.chunkSettings.chunkSize.x, worldSettings.worldSizeInChunks.y * worldSettings.chunkSettings.chunkSize.y, worldSettings.worldSizeInChunks.z * worldSettings.chunkSettings.chunkSize.z);
        }
    }

    public World(WorldSettings settings, Transform root) 
    {
        worldSettings = settings;
        Random.InitState(worldSettings.seed);
        this.root = root;
    }

    public void Init(Transform player, float playerHeight) 
    {
        spawnPosition = GetSpawnPosition(playerHeight);

        GenerateWorld();
        player.position = spawnPosition;
        lastPlayerChunkCoordinates = GetChunkCoordinatesFromWorldPosition(player.position);
    }

    public Vector3 GetSpawnPosition(float playerHeight) 
    {
        return new Vector3(worldSettings.worldSizeInChunks.x * worldSettings.chunkSettings.chunkSize.x * worldSettings.chunkSettings.voxelSize.x / 2,
                   playerHeight,
                   worldSettings.worldSizeInChunks.z * worldSettings.chunkSettings.chunkSize.z * worldSettings.chunkSettings.voxelSize.z / 2);
    }

    public void GenerateWorld()
    {
        chunks = new ChunkData[(int)worldSettings.worldSizeInChunks.x, (int)worldSettings.worldSizeInChunks.y, (int)worldSettings.worldSizeInChunks.z];

        int startX = (int)(worldSettings.worldSizeInChunks.x / 2) - (int)worldSettings.maxChunkViewDistance.x;
        int endX = (int)(worldSettings.worldSizeInChunks.x / 2) + (int)worldSettings.maxChunkViewDistance.x;

        int startY = (int)(worldSettings.worldSizeInChunks.y / 2) - (int)worldSettings.maxChunkViewDistance.y;
        int endY = (int)(worldSettings.worldSizeInChunks.y / 2) + (int)worldSettings.maxChunkViewDistance.y;

        int startZ = (int)(worldSettings.worldSizeInChunks.z / 2) - (int)worldSettings.maxChunkViewDistance.z;
        int endZ = (int)(worldSettings.worldSizeInChunks.z / 2) + (int)worldSettings.maxChunkViewDistance.z;

        ChunkCoordinates start = ClampToChunkCoordinateToWorld(startX, startY, startZ);
        ChunkCoordinates end = ClampToChunkCoordinateToWorld(endX, endY, endZ);

        for (int i = start.x; i < end.x; i++)
        {
            for (int j = start.y; j < end.y; j++)
            {
                for (int k = start.z; k < end.z; k++)
                {                    
                    chunks[i, j, k] = GenerateChunkFromChunkCoordinates(i, j, k, true);

                    activeChunks.Add(new ChunkCoordinates(i, j, k));
                }
            }
        }

        for (int i = start.x; i < end.x; i++)
        {
            for (int j = start.y; j < end.y; j++)
            {
                for (int k = start.z; k < end.z; k++)
                {
                    SetupChunkGameObjects(i, j, k);
                }
            }
        }
    }

    public ChunkCoordinates ClampToChunkCoordinateToWorld(int x, int y, int z) 
    {
        return new ChunkCoordinates(Mathf.Clamp(x, 0, (int)worldSettings.worldSizeInChunks.x), Mathf.Clamp(y, 0, (int)worldSettings.worldSizeInChunks.y), Mathf.Clamp(z, 0, (int)worldSettings.worldSizeInChunks.z));
    }

    public ChunkCoordinates GetChunkCoordinatesFromWorldPosition(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x / worldSettings.chunkSettings.chunkSize.x);
        int y = Mathf.FloorToInt(position.y / worldSettings.chunkSettings.chunkSize.y);
        int z = Mathf.FloorToInt(position.z / worldSettings.chunkSettings.chunkSize.z);

        ChunkCoordinates coordinates = new ChunkCoordinates(x, y, z);

        return coordinates;
    }

    public void UpdateWorld(Transform player)
    {
        var chunkCoordinates = GetChunkCoordinatesFromWorldPosition(player.position);
        currentPlayerChunkCoordinates = chunkCoordinates;

        if (lastPlayerChunkCoordinates == chunkCoordinates)
        {
            return;
        }

        int minActiveX = chunkCoordinates.x - (int)worldSettings.maxChunkViewDistance.x;
        int maxActiveX = chunkCoordinates.x + (int)worldSettings.maxChunkViewDistance.x;

        int minActiveY = chunkCoordinates.y - (int)worldSettings.maxChunkViewDistance.y;
        int maxActiveY = chunkCoordinates.y + (int)worldSettings.maxChunkViewDistance.y;

        int minActiveZ = chunkCoordinates.z - (int)worldSettings.maxChunkViewDistance.z;
        int maxActiveZ = chunkCoordinates.z + (int)worldSettings.maxChunkViewDistance.z;

        ChunkCoordinates minActive = ClampToChunkCoordinateToWorld(minActiveX, minActiveY, minActiveZ);
        ChunkCoordinates maxActive = ClampToChunkCoordinateToWorld(maxActiveX, maxActiveY, maxActiveZ);

        for (int i = 0; i < activeChunks.Count; i++)
        {
            if ((activeChunks[i].x < minActive.x || activeChunks[i].x >= maxActive.x) ||
                (activeChunks[i].y < minActive.y || activeChunks[i].y >= maxActive.y) ||
                (activeChunks[i].z < minActive.z || activeChunks[i].z >= maxActive.z))
            {
                chunks[activeChunks[i].x, activeChunks[i].y, activeChunks[i].z].IsActive = false;
                activeChunks.RemoveAt(i);
                i--;
            }
        }

        for (int i = minActive.x; i < maxActive.x; i++)
        {
            for (int j = minActive.y; j < maxActive.y; j++)
            {
                for (int k = minActive.z; k < maxActive.z; k++)
                {
                    if (IsChunkInWorld(i, j, k))
                    {
                        activeChunks.Add(new ChunkCoordinates(i, j, k));

                        if (chunks[i, j, k] == null)
                        {
                            chunksToCreate.Add(new ChunkCoordinates(i, j, k));
                            chunks[i, j, k] = GenerateChunkFromChunkCoordinates(i, j, k, false);
                        }
                        else
                        {
                            chunks[i, j, k].IsActive = true;
                        }                        
                    }
                }
            }
        }

        lastPlayerChunkCoordinates = chunkCoordinates;
    }    

    public bool IsChunkInWorld(int x, int y, int z)
    {
        if (x >= 0 && x < worldSettings.worldSizeInChunks.x && y >= 0 && y < worldSettings.worldSizeInChunks.y && z >= 0 && z < worldSettings.worldSizeInChunks.z)
        {
            return true;
        }

        return false;
    }

    public bool IsVoxelInWorld(Vector3 worldPosition)
    {
        if (worldPosition.x >= 0 && worldPosition.x < WorldSizeInVoxels.x && worldPosition.y >= 0 && worldPosition.y < WorldSizeInVoxels.y && worldPosition.z >= 0 && worldPosition.z < WorldSizeInVoxels.z)
        {
            return true;
        }

        return false;
    }

    public bool IsWorldVoxelSolid(Vector3 voxelWorldPosition) 
    {
        if (IsVoxelInWorld(voxelWorldPosition))
        {
            var chunkPosition = GetChunkCoordinatesFromWorldPosition(voxelWorldPosition);
            if (chunks[chunkPosition.x, chunkPosition.y, chunkPosition.z] != null)
            {
                var voxelPosition = GetChunkVoxelPositionFromWorldCoordinates(voxelWorldPosition);
                var chunk = chunks[chunkPosition.x, chunkPosition.y, chunkPosition.z];
                if (chunk != null && chunk.isInitialized)
                {
                    var type = chunk.GetVoxelType((int)voxelPosition.x, (int)voxelPosition.y, (int)voxelPosition.z);

                    if (type != null)
                    {
                        return type.IsSolid;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        return false;
    }

    public Vector3 GetChunkVoxelPositionFromWorldCoordinates(Vector3 worldPosition) 
    {
        int x = (int)worldPosition.x % (int)worldSettings.chunkSettings.chunkSize.x;

        int y = (int)worldPosition.y % (int)worldSettings.chunkSettings.chunkSize.y;

        int z = (int)worldPosition.z % (int)worldSettings.chunkSettings.chunkSize.z;

        return new Vector3(x, y, z);
    }

    public VoxelType DetermineVoxelType(Vector3 positionInVoxels) 
    {
        if (IsVoxelInWorld(positionInVoxels) == false)
        {
            return worldSettings.chunkSettings.voxelTypeCollection.GetVoxelType("Air");
        }       

        float randomHeightPercentage = Noise.Get2DPerlin(positionInVoxels, WorldSizeInVoxels, 0, worldSettings.biomeAttributes.noiseScale);
        int terrainHeight = Mathf.FloorToInt(worldSettings.biomeAttributes.terrainHeight * randomHeightPercentage) + worldSettings.biomeAttributes.solidGroundHeight;

        VoxelType voxelType;
        //voxelType = worldSettings.chunkSettings.voxelTypeCollection.GetVoxelType("Stone");
        //return voxelType;

        if (positionInVoxels.y < terrainHeight)
        {
            int bedrockThreshold = (terrainHeight / 16);
            int stoneThreshold = (terrainHeight * 5 / 6);
            
            if (positionInVoxels.y <= bedrockThreshold)
            {
                voxelType = worldSettings.chunkSettings.voxelTypeCollection.GetVoxelType("Bedrock");
            }
            else if (positionInVoxels.y > bedrockThreshold && positionInVoxels.y <= stoneThreshold)
            {
                voxelType = worldSettings.chunkSettings.voxelTypeCollection.GetVoxelType("Stone");
            }
            else
            {
                voxelType = worldSettings.chunkSettings.voxelTypeCollection.GetVoxelType("Dirt");
            }           
        }
        else if (positionInVoxels.y == terrainHeight)
        {
            voxelType = worldSettings.chunkSettings.voxelTypeCollection.GetVoxelType("Grass");
        }
        else
        {
            voxelType = worldSettings.chunkSettings.voxelTypeCollection.GetVoxelType("Air");
        }

        if (voxelType.TypeName == "Stone")
        {
            for (int i = 0; i < worldSettings.biomeAttributes.lodes.Count; i++)
            {
                Lode lode = worldSettings.biomeAttributes.lodes[i];

                if (positionInVoxels.y > lode.minHeight && positionInVoxels.y < lode.maxHeight)
                {
                    if (Noise.Get3DPerlin(positionInVoxels, lode.noiseOffset, lode.noiseScale, lode.noiseThreshold))
                    {
                        voxelType = lode.voxelType;
                    }
                }
            }
        }

        return voxelType;
    }

    public void EditVoxel(Vector3 voxelWorldPosition, VoxelType voxelType) 
    {
        if (IsVoxelInWorld(voxelWorldPosition))
        {
            var chunkPosition = GetChunkCoordinatesFromWorldPosition(voxelWorldPosition);
            if (chunks[chunkPosition.x, chunkPosition.y, chunkPosition.z] != null)
            {
                var voxelPosition = GetChunkVoxelPositionFromWorldCoordinates(voxelWorldPosition);
                var chunk = chunks[chunkPosition.x, chunkPosition.y, chunkPosition.z];
                if (chunk != null && chunk.isInitialized)
                {
                    chunk.SetVoxelType((int)voxelPosition.x, (int)voxelPosition.y, (int)voxelPosition.z, voxelType);

                    chunksToUpdate.Add(chunk);
                    UpdateAdjacentChunks(chunkPosition);
                }                
            }
        }
    }

    private void UpdateAdjacentChunks(ChunkCoordinates chunkCoordinates) 
    {
        int up = chunkCoordinates.y + 1;
        int down = chunkCoordinates.y - 1;

        int front = chunkCoordinates.z + 1;
        int back = chunkCoordinates.z - 1;

        int right = chunkCoordinates.x + 1;
        int left = chunkCoordinates.x - 1;

        if (IsChunkInWorld(chunkCoordinates.x, up, chunkCoordinates.z))
        {
            if (chunks[chunkCoordinates.x, up, chunkCoordinates.z] != null && chunks[chunkCoordinates.x, up, chunkCoordinates.z].isInitialized)
            {
                chunksToUpdate.Add(chunks[chunkCoordinates.x, up, chunkCoordinates.z]);
            }            
        }

        if (IsChunkInWorld(chunkCoordinates.x, down, chunkCoordinates.z))
        {
            if (chunks[chunkCoordinates.x, down, chunkCoordinates.z] != null && chunks[chunkCoordinates.x, down, chunkCoordinates.z].isInitialized)
            {
                chunksToUpdate.Add(chunks[chunkCoordinates.x, down, chunkCoordinates.z]);
            }
        }

        if (IsChunkInWorld(chunkCoordinates.x, chunkCoordinates.y, front))
        {
            if (chunks[chunkCoordinates.x, chunkCoordinates.y, front] != null && chunks[chunkCoordinates.x, chunkCoordinates.y, front].isInitialized)
            {
                chunksToUpdate.Add(chunks[chunkCoordinates.x, chunkCoordinates.y, front]);
            }
        }

        if (IsChunkInWorld(chunkCoordinates.x, chunkCoordinates.y, back))
        {
            if (chunks[chunkCoordinates.x, chunkCoordinates.y, back] != null && chunks[chunkCoordinates.x, chunkCoordinates.y, back].isInitialized)
            {
                chunksToUpdate.Add(chunks[chunkCoordinates.x, chunkCoordinates.y, back]);
            }
        }

        if (IsChunkInWorld(right, chunkCoordinates.y, chunkCoordinates.z))
        {
            if (chunks[right, chunkCoordinates.y, chunkCoordinates.z] != null && chunks[right, chunkCoordinates.y, chunkCoordinates.z].isInitialized)
            {
                chunksToUpdate.Add(chunks[right, chunkCoordinates.y, chunkCoordinates.z]);
            }
        }

        if (IsChunkInWorld(left, chunkCoordinates.y, chunkCoordinates.z))
        {
            if (chunks[left, chunkCoordinates.y, chunkCoordinates.z] != null && chunks[left, chunkCoordinates.y, chunkCoordinates.z].isInitialized)
            {
                chunksToUpdate.Add(chunks[left, chunkCoordinates.y, chunkCoordinates.z]);
            }
        }
    }

    private ChunkData GenerateChunkFromChunkCoordinates(int x, int y, int z, bool initializeOnLoad)
    {
        ChunkCoordinates coordinates = new ChunkCoordinates(x, y, z);

        ChunkData chunk = new ChunkData(this, coordinates, initializeOnLoad);

        return chunk;
    }

    public void InitializeChunk(int x, int y, int z) 
    {
        if (IsChunkInWorld(x, y, z))
        {
            if (chunks[x, y, z].isInitialized == false)
            {
                chunks[x, y, z].Init();
            }
        }
    }

    public void SetupChunkGameObjects(int x, int y, int z) 
    {
        var chunk = chunks[x, y, z];
        
        if (chunk.isInitialized == false)
        {
            Debug.Log("Trying to setup mesh for uninitialized chunk");
            return;
        }

        var chunkGameObject = new GameObject("Chunk_" + chunk.chunkCoordinates.x + "," + chunk.chunkCoordinates.y + "," + chunk.chunkCoordinates.z);
        chunkGameObject.transform.SetParent(root);
        chunkGameObject.transform.position = new Vector3(chunkGameObject.transform.position.x + 8, chunkGameObject.transform.position.y + WorldSizeInVoxels.y / 2, chunkGameObject.transform.position.z + 8);
        chunk.chunkObject = chunkGameObject;

        var meshFilter = chunkGameObject.AddComponent<MeshFilter>();
        var meshRenderer = chunkGameObject.AddComponent<MeshRenderer>();

        chunk.SetupMesh(ref meshFilter);

        worldSettings.chunkSettings.voxelMaterial.SetTexture(worldSettings.chunkSettings.textureAtlas.Name, worldSettings.chunkSettings.textureAtlas.Atlas);

        meshRenderer.sharedMaterial = worldSettings.chunkSettings.voxelMaterial;
    }
}
