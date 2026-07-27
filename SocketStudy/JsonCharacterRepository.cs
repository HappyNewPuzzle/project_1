using System.Text.Json;

public sealed class JsonCharacterRepository : ICharacterRepository
{
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };

    public JsonCharacterRepository(string filePath)
    {
        this.filePath = Path.GetFullPath(filePath);
    }

    public async Task SaveAsync(CharacterSaveData character, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Dictionary<long, CharacterSaveData> characters = await ReadAllAsync(cancellationToken);
            characters[character.PlayerId] = character;
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = filePath + ".tmp";
            await using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, characters, options, cancellationToken);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<CharacterSaveData?> LoadAsync(long playerId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Dictionary<long, CharacterSaveData> characters = await ReadAllAsync(cancellationToken);
            return characters.GetValueOrDefault(playerId);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Dictionary<long, CharacterSaveData>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new Dictionary<long, CharacterSaveData>();
        }

        await using FileStream stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<Dictionary<long, CharacterSaveData>>(
            stream,
            options,
            cancellationToken) ?? new Dictionary<long, CharacterSaveData>();
    }
}
