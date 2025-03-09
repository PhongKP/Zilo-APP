using AuthenticationAppMVC.Data;
using AuthenticationAppMVC.Models;
using AuthenticationAppMVC.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAppMVC.Services.Impl
{
    public class FriendsServiceImpl : IFriendsService
    {
        private readonly AppDBContext _dbcontext;
        private readonly ILogger<FriendsServiceImpl> _logger;

        public FriendsServiceImpl(AppDBContext dbcontext, ILogger<FriendsServiceImpl> logger)
        {
            _dbcontext = dbcontext;
            _logger = logger;
        }

        public async Task<bool> AcceptFriendRequestAsync(string requestId, string currentUserId)
        {
            try
            {
                // Kiểm tra xem lời mời kết bạn có tồn tại không
                var existingFriendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(f => f.Id == requestId);
                if (existingFriendRequest == null)
                {
                    return false;
                }

                existingFriendRequest.requestStatus = FriendRequestStatus.Accepted;
                existingFriendRequest.AcceptedAt = DateTimeOffset.UtcNow;

                // Tạo một friendship mới giữa 2 người.
                FriendShip friendShip = new FriendShip
                {
                    User1Id = existingFriendRequest.SenderId,
                    User2Id = existingFriendRequest.ReceiverId,
                    friendShipStatus = FriendShipStatus.Active
                };
                _dbcontext.FriendShips.Add(friendShip);
                await _dbcontext.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error accepting friend request");
                return false;
            }
        }

        public async Task<bool> CancelFriendRequestAsync(string requestId, string currentUserId)
        {
            try
            {
                // Kiểm tra xem lời mời kết bạn có tồn tại không
                var existingFriendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(
                                                        f => f.Id == requestId && f.SenderId == currentUserId);
                if (existingFriendRequest == null)
                {
                    return false;
                }

                _dbcontext.FriendRequests.Remove(existingFriendRequest);
                await _dbcontext.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error canceling friend request");
                return false;
            }
        }

        public async Task<bool> DeclineFriendRequestAsync(string requestId, string currentUserId)
        {
            try
            {
                // Kiểm tra xem lời mời kết bạn có tồn tại không
                var existingFriendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(
                                                        f => f.Id == requestId && f.ReceiverId == currentUserId);
                if (existingFriendRequest == null)
                {
                    return false;
                }

                existingFriendRequest.requestStatus = FriendRequestStatus.Rejected;
                existingFriendRequest.AcceptedAt = DateTimeOffset.UtcNow;
                await _dbcontext.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error declining friend request");
                return false;
            }
        }

        public async Task<FriendRequest?> GetFriendRequestByIdAsync(string requestId)
        {
            try
            {
                var friendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(f => f.Id == requestId);
                if (friendRequest == null)
                {
                    return null;
                }
                return friendRequest;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error getting friend request by ID");
                return null;
            }
        }

        public async Task<List<User>?> GetFriendsAsync(string userId)
        {
            try
            {
                var friends = await _dbcontext.FriendShips
                    .Where(f => f.User1Id == userId || f.User2Id == userId)
                    .Select(f => f.User1Id == userId ? f.User2 : f.User1)
                    .ToListAsync();
                return friends;

            } catch (Exception e)
            {
                _logger.LogError(e, "Error getting friends");
                return null;
            }
        }

        public async Task<string> GetFriendshipStatusAsync(string userId1, string userId2)
        {
            try
            {
                var existingFriendRequest = await _dbcontext.FriendRequests.FirstOrDefaultAsync(
                    f => (f.SenderId == userId1 && f.ReceiverId == userId2) ||
                         (f.SenderId == userId2 && f.ReceiverId == userId1));
                
                var existingFriendShip = await _dbcontext.FriendShips.FirstOrDefaultAsync(
                    f => (f.User1Id == userId1 && f.User2Id == userId2) ||
                         (f.User1Id == userId2 && f.User2Id == userId1));

                if (existingFriendShip == null)
                {
                    return "NotFriends";
                }

                if (existingFriendRequest != null)
                {
                    if (existingFriendRequest.requestStatus == FriendRequestStatus.Pending)
                    {
                        if (existingFriendRequest.SenderId == userId1)
                        {
                            return "RequestSent";
                        }
                        else
                        {
                            return "RequestReceived";
                        }
                    }

                    if (existingFriendShip.friendShipStatus == FriendShipStatus.Active && 
                        existingFriendRequest.requestStatus == FriendRequestStatus.Accepted)
                    {
                        return "Friends";
                    }
                }
                return "NotFriends";
            } catch (Exception e)
            {
                _logger.LogError(e, "Error getting friendship status");
                return "Error";
            }
        }

        public async Task<List<FriendRequestDTO>?> GetPendingFriendRequestsAsync(string userId)
        {
            try
            {
                var pendingFriendRequests = await _dbcontext.FriendRequests
                    .Where(f => f.ReceiverId == userId && f.requestStatus == FriendRequestStatus.Pending)
                    .Include(fr => fr.Sender)
                    .Select(fr => new FriendRequestDTO
                    {
                        id = fr.Id,                
                        SenderId = fr.SenderId,
                        ReceiverId = fr.ReceiverId,
                        RequestStatus = fr.requestStatus,
                        Sender = fr.Sender,
                        CreatedAt = fr.CreatedAt,
                        AcceptedAt = fr.AcceptedAt
                    })
                    .ToListAsync();
                return pendingFriendRequests;

            } catch (Exception e)
            {
                _logger.LogError(e, "Error getting pending friend requests");
                return null;
            }
        }

        public async Task<List<FriendRequestDTO>?> GetSentFriendRequestsAsync(string userId)
        {
            try
            {
                var sendingRequests = await _dbcontext.FriendRequests
                    .Where(fr => fr.SenderId == userId && fr.requestStatus == FriendRequestStatus.Pending)
                    .Include(fr => fr.Receiver)
                    .Select(fr => new FriendRequestDTO
                    {
                        id = fr.Id,
                        ReceiverId = fr.ReceiverId,
                        RequestStatus = fr.requestStatus,
                        Receiver = fr.Receiver,
                        CreatedAt = fr.CreatedAt,
                        AcceptedAt = fr.AcceptedAt
                    })
                    .ToListAsync();
                return sendingRequests;
            } catch (Exception e)
            {
                _logger.LogError(e, "Error getting sent friend requests");
                return null;
            }
        }

        public async Task<bool> RemoveFriendAsync(string userId1, string userId2)
        {
            try
            {
                var existingFriendShip = await _dbcontext.FriendShips.FirstOrDefaultAsync(
                    f => f.User1Id == userId1 && f.User2Id == userId2 ||
                         f.User1Id == userId2 && f.User2Id == userId1);
                if (existingFriendShip == null)
                {
                    return false;
                }

                _dbcontext.FriendShips.Remove(existingFriendShip);
                await _dbcontext.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error removing friend");
                return false;
            }
        }

        public async Task<List<User>?> SearchFriendsAsync(string userId, string searchTerm)
        {
            try
            {
                var friendships = await _dbcontext.FriendShips
                        .Where(f => f.User1Id == userId || f.User2Id == userId)
                        .ToListAsync();

                var friendIds = friendships
                    .Select(f => f.User1Id == userId ? f.User2Id : f.User1Id)
                    .ToList();

                var users = await _dbcontext.Users
                    .Where(u => friendIds.Contains(u.Id) &&
                        (u.UserName.Contains(searchTerm) ||
                         u.FullName.Contains(searchTerm) ||
                         u.Email.Contains(searchTerm)))
                    .ToListAsync();
                return users;
            } catch (Exception e)
            {
                _logger.LogError(e, "Error searching friends");
                return null;
            }
        }

        public async Task<bool> SendFriendRequestAsync(string senderId, string receiverId)
        {
            try
            {
                var existingFriendShip = await _dbcontext.FriendShips.FirstOrDefaultAsync(
                    f => f.User1Id == senderId && f.User2Id == receiverId ||
                         f.User1Id == receiverId && f.User2Id == senderId);

                if (existingFriendShip != null)
                {
                    return false;
                }

                FriendRequest friendRequest = new FriendRequest
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    requestStatus = FriendRequestStatus.Pending
                };

                _dbcontext.FriendRequests.Add(friendRequest);
                await _dbcontext.SaveChangesAsync();
                return true;

            } catch (Exception e)
            {
                _logger.LogError(e, "Error sending friend request");
                return false;
            }
        }
    }
}
