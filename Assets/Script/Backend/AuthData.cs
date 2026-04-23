using UnityEngine;

[CreateAssetMenu(fileName = "AuthData", menuName = "Backend/AuthData")]
public class AuthData : ScriptableObject
{
    public string Token;
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public void Clear() => Token = string.Empty;
}
