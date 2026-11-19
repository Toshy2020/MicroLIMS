namespace MicroLIMS.Shared.Messages;

// Discussion.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string DiscussionAttachmentInvalid = "Discussion.AttachmentInvalid";
    public const string DiscussionCommentContentEmpty = "Discussion.CommentContentEmpty";
    public const string DiscussionContentRequired = "Discussion.ContentRequired";
    public const string DiscussionConversationInactiveUsersNotAllowed = "Discussion.ConversationInactiveUsersNotAllowed";
    public const string DiscussionConversationRequiresParticipant = "Discussion.ConversationRequiresParticipant";
    public const string DiscussionFileIntegrityFailed = "Discussion.FileIntegrityFailed";
    public const string DiscussionInitialMessageRequired = "Discussion.InitialMessageRequired";
    public const string DiscussionMessageContentEmpty = "Discussion.MessageContentEmpty";

    private static Dictionary<string, string[]> DiscussionParameters() => new()
    {
        [DiscussionAttachmentInvalid] = ["message"],
        [DiscussionCommentContentEmpty] = [],
        [DiscussionContentRequired] = [],
        [DiscussionConversationInactiveUsersNotAllowed] = [],
        [DiscussionConversationRequiresParticipant] = [],
        [DiscussionFileIntegrityFailed] = [],
        [DiscussionInitialMessageRequired] = [],
        [DiscussionMessageContentEmpty] = [],
    };
}
