// 닉네임과 방 이름처럼 사용자가 직접 입력하는 이름 규칙을 모아둡니다.
public static class NameRules
{
    // 이름에 허용된 문자 집합입니다.
    private const string AllowedNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";

    // 이름의 최대 길이입니다.
    public const int MaxNameLength = 20;

    // 닉네임 문자 규칙 안내 문구입니다.
    public const string NicknameCharacterRuleMessage = "Nickname can contain only letters, numbers, '-' and '_'.";

    // 방 이름 문자 규칙 안내 문구입니다.
    public const string RoomNameCharacterRuleMessage = "Room name can contain only letters, numbers, '-' and '_'.";

    // 이름이 허용된 문자로만 이루어졌는지 확인합니다.
    public static bool HasOnlyAllowedCharacters(string name)
    {
        // 모든 문자가 허용된 문자 집합 안에 있는지 확인합니다.
        return name.All(character => AllowedNameCharacters.Contains(character));
    }
}
