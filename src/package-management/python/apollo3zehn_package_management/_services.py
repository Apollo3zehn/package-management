import asyncio
import inspect
import json
import logging
import os
import traceback
import uuid
from logging import Logger
from pathlib import Path
from types import ModuleType
from typing import (Awaitable, Callable, Dict, Generic, List, Optional, Type,
                    TypeVar, cast)
from uuid import UUID

from ._encoder import JsonEncoder
from ._package_management import PackageController
from ._typedefs import PackageReference

T = TypeVar("T")

class PackageService:

    _lock = asyncio.Lock()
    _cache: Optional[dict[UUID, PackageReference]] = None

    def __init__(self, config_folder_path: str):
        self._config_folder_path = config_folder_path

    def put(self, package_reference: PackageReference) -> Awaitable[UUID]:

        return self._interact_with_package_reference_map(
            lambda package_reference_map: self._put_internal(package_reference, package_reference_map),
            save_changes=True
        )
        
    def _put_internal(self, package_reference: PackageReference, package_reference_map: dict[UUID, PackageReference]) -> UUID:

        id = uuid.uuid4()
        package_reference_map[id] = package_reference

        return id

    def get(self, package_reference_id: UUID) -> Awaitable[Optional[PackageReference]]:

        return self._interact_with_package_reference_map(
            lambda package_reference_map: package_reference_map.get(package_reference_id),
            save_changes=False
        )
    
    def delete(self, package_reference_id: UUID) -> Awaitable:

        return self._interact_with_package_reference_map(
            lambda package_reference_map: package_reference_map.pop(package_reference_id, None),
            save_changes=True
        )

    def get_all(self) -> Awaitable[dict[UUID, PackageReference]]:

        return self._interact_with_package_reference_map(
            lambda package_reference_map: package_reference_map,
            save_changes=False
        )

    def _get_package_reference_map(self) -> dict[UUID, PackageReference]:
    
        if self._cache is None:
        
            folder_path = self._config_folder_path
            package_references_file_path = os.path.join(folder_path, "packages.json")

            if (os.path.exists(package_references_file_path)):

                with open(package_references_file_path, "r") as file:
                    json_value = json.load(file)
                
                self._cache = JsonEncoder.decode(dict[UUID, PackageReference], json_value)

            else:
                return {}
        
        return self._cache

    async def _interact_with_package_reference_map(
        self, 
        func: Callable[[dict[UUID, PackageReference]], T], 
        save_changes: bool
    ) -> T:
    
        async with self._lock:

            package_reference_map = self._get_package_reference_map()
            result = func(package_reference_map)

            if save_changes:

                folder_path = self._config_folder_path
                package_references_file_path = os.path.join(folder_path, "packages.json")

                Path(folder_path).mkdir(parents=True, exist_ok=True)

                json_value = JsonEncoder.encode(package_reference_map)

                with open(package_references_file_path, "w") as file:
                    json.dump(json_value, file, indent=2)

            return result

class ExtensionHive(Generic[T]):

    _package_controller_map: Optional[dict[UUID, tuple[PackageController, list[Type]]]] = None

    def __init__(self, packages_folder_path: str, logger: Logger):
        
        self._packages_folder_path = packages_folder_path
        self._logger = logger

    async def load_packages(self, package_reference_map: Dict[UUID, PackageReference]):

        # Clean up
        if self._package_controller_map is not None:

            self._logger.debug("Unload previously loaded packages")

            for controller, _ in self._package_controller_map.values():
                controller.unload()

            self._package_controller_map = None

        # Build new
        package_controller_map: dict[UUID, tuple[PackageController, list[Type]]] = {}

        for id, package_reference in package_reference_map.items():

            package_controller = PackageController(package_reference, logging.getLogger("PackageController"))

            try:

                self._logger.debug("Load package")

                module = await package_controller.load(self._packages_folder_path)
                types = self._scan_module(module, is_builtin_provider=package_reference.provider == PackageController.BUILTIN_PROVIDER)
                package_controller_map[id] = (package_controller, types)

            except Exception as ex:

                self._logger.error(f"Loading package failed: {ex}\n{traceback.format_exc()}")

        self._package_controller_map = package_controller_map

    def get_extensions(self) -> List[Type]:

        if self._package_controller_map is None:
            return []

        return [type for _, (_, types) in self._package_controller_map.items() for type in types]

    def get_extension_type(self, full_name: str) -> Type:

        type_info = self._get_type_info(full_name)

        if type_info is None:
            raise Exception(f"Could not find extension {full_name} of type {self.__orig_class__.__args__[0].__name__}.") # pyright: ignore
        
        return type_info[2]

    def _get_type_info(self, full_name: str) -> Optional[tuple[UUID,PackageController, Type]]:

        if self._package_controller_map is None:
            return None

        type_infos = [(id, controller, type) for id, (controller, types) in self._package_controller_map.items() for type in types]

        for id, controller, type in type_infos:

            if f"{type.__module__}.{type.__name__}" == full_name:
                return (id, controller, type)

        return None

    def _scan_module(self, module: ModuleType, is_builtin_provider: bool) -> List[Type]:

        if is_builtin_provider:
            raise Exception("The builtin provider is not supported.")

        value_of_T: Type = self.__orig_class__.__args__[0] # pyright: ignore

        found_types = [
            member[1] for member in inspect.getmembers(module) if \
                inspect.isclass(member[1]) and \
                member[1] is not value_of_T and \
                issubclass(member[1], value_of_T)
        ]

        return found_types