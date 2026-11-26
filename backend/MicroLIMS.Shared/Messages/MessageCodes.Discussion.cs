namespace MicroLIMS.Shared.Messages;

// Discussion.* refusal codes and the placeholders their sentences use (see MessageCodes.cs).
public static partial class MessageCodes
{
    public const string DiscussionAttachmentCountExceeded = "Discussion.AttachmentCountExceeded";
    public const string DiscussionAttachmentEmpty = "Discussion.AttachmentEmpty";
    public const string DiscussionAttachmentTooLarge = "Discussion.AttachmentTooLarge";
    public const string DiscussionAttachmentTypeUnsupported = "Discussion.AttachmentTypeUnsupported";
    public const string DiscussionAttachmentSignatureMismatch = "Discussion.AttachmentSignatureMismatch";
    public const string DiscussionCommentContentEmpty = "Discussion.CommentContentEmpty";
    public const string DiscussionContentRequired = "Discussion.ContentRequired";
    public const string DiscussionConversationInactiveUsersNotAllowed = "Discussion.ConversationInactiveUsersNotAllowed";
    public const string DiscussionConversationRequiresParticipant = "Discussion.ConversationRequiresParticipant";
    public const string DiscussionFileIntegrityFailed = "Discussion.FileIntegrityFailed";
    public const string DiscussionInitialMessageRequired = "Discussion.InitialMessageRequired";
    public const string DiscussionMessageContentEmpty = "Discussion.MessageContentEmpty";

    private static Dictionary<string, string[]> DiscussionParameters() => new()
    {
        [DiscussionAttachmentCountExceeded] = ["maxFiles", "fileCount"],
        [DiscussionAttachmentEmpty] = ["fileName"],
        [DiscussionAttachmentTooLarge] = ["fileName", "maxMb"],
        [DiscussionAttachmentTypeUnsupported] = ["fileName", "allowedTypes"],
        [DiscussionAttachmentSignatureMismatch] = ["fileName", "fileType"],
        [DiscussionCommentContentEmpty] = [],
        [DiscussionContentRequired] = [],
        [DiscussionConversationInactiveUsersNotAllowed] = [],
        [DiscussionConversationRequiresParticipant] = [],
        [DiscussionFileIntegrityFailed] = [],
        [DiscussionInitialMessageRequired] = [],
        [DiscussionMessageContentEmpty] = [],
    };
}
