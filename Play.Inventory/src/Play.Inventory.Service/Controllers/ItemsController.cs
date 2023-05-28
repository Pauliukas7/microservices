using Microsoft.AspNetCore.Mvc;
using Play.Common;
using Play.Inventory.Service.Clients;
using Play.Inventory.Service.Dtos;
using Play.Inventory.Service.Entities;

namespace Play.Inventory.Service.Controllers
{
    [ApiController]
    [Route("items")]
    public class ItemsController : ControllerBase
    {
        private readonly IRepository<InventoryItem> itemsRepository;
        private readonly CatalogClient catalogClient;

        public ItemsController(
            IRepository<InventoryItem> itemsRepository,
            CatalogClient catalogClient
        )
        {
            this.itemsRepository = itemsRepository;
            this.catalogClient = catalogClient;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest();
            }

            var catalogItems = await catalogClient.GetCatalogItemsAsync();
            var InventoryItemEntities = await itemsRepository.GetAllAsync(
                item => item.UserId == userId
            );

            var inventoryItemDtos = InventoryItemEntities.Select(InventoryItem =>
            {
                var catalogItem = catalogItems.Single(
                    catalogItem => catalogItem.Id == InventoryItem.CatalogItemId
                );
                return InventoryItem.AsDto(catalogItem.Name, catalogItem.Description);
            });

            return Ok(inventoryItemDtos);
        }

        [HttpPost]
        public async Task<ActionResult> PostAsync(GrantItemsDto grantItemsDto)
        {
            var InventoryItem = await itemsRepository.GetAsync(
                item =>
                    item.UserId == grantItemsDto.UserId
                    && item.CatalogItemId == grantItemsDto.CatalogItemId
            );

            if (InventoryItem == null)
            {
                InventoryItem = new InventoryItem
                {
                    CatalogItemId = grantItemsDto.CatalogItemId,
                    UserId = grantItemsDto.UserId,
                    Quantity = grantItemsDto.Quantity,
                    AcquiredDate = DateTimeOffset.UtcNow
                };
                await itemsRepository.CreateAsync(InventoryItem);
            }
            else
            {
                InventoryItem.Quantity += grantItemsDto.Quantity;
                await itemsRepository.UpdateAsync(InventoryItem);
            }
            return Ok();
        }
    }
}
