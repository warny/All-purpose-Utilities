namespace Utils.Parser.Runtime;

public interface ICharStream
{
    int Position { get; }
    bool IsEnd { get; }
    char Peek(int offset = 0);
    void Consume(int count = 1);

    // Pour le parallel scan : sauvegarde/restauration sans allocation
    int SavePosition();
    void RestorePosition(int savedPosition);
}
