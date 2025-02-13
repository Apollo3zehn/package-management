import json
import os
import tempfile
import uuid
from typing import cast

from apollo3zehn_package_management import PackageReference, PackageService
from apollo3zehn_package_management._encoder import JsonEncoder


async def can_create_package_reference_test():
    
    # Arrange
    with tempfile.NamedTemporaryFile(delete=True) as temp_file:
        config_folder_path = temp_file.name
        
    package_service = PackageService(config_folder_path)

    package_reference = PackageReference(
        provider="foo",
        configuration=cast(dict[str, str], None)
    )

    # Act
    expected_id = await package_service.put(package_reference)

    # Assert
    with open(os.path.join(config_folder_path, "packages.json"), "r") as file:
        actual_package_reference_map = JsonEncoder.decode(dict[uuid.UUID, PackageReference], json.load(file))
        
    entry = list(actual_package_reference_map.items())[0]

    assert entry[0] == expected_id
    assert entry[1].provider == "foo"
    assert entry[1].configuration is None

async def can_get_package_reference_test():

    # Arrange
    id1 = uuid.uuid4()
    id2 = uuid.uuid4()

    package_reference_map = {
        id1: PackageReference(
            provider="foo",
            configuration=cast(dict[str, str], None)
        ),
        id2: PackageReference(
            provider="bar",
            configuration=cast(dict[str, str], None)
        )
    }

    json_value = JsonEncoder.encode(package_reference_map)

    with tempfile.NamedTemporaryFile(delete=True) as temp_file:
        config_folder_path = temp_file.name

    os.makedirs(config_folder_path)

    with open(os.path.join(config_folder_path, "packages.json"), "w") as file:
        json.dump(json_value, file)

    package_service = PackageService(config_folder_path)

    # Act
    actual = await package_service.get(id2)

    # Assert
    assert json.dumps(JsonEncoder.encode(actual)) == \
        json.dumps(JsonEncoder.encode(package_reference_map[id2]))
    
async def can_try_update_package_reference_test():

    # Arrange
    id1 = uuid.uuid4()
    id2 = uuid.uuid4()

    package_reference_map = {
        id1: PackageReference(
            provider="foo",
            configuration=cast(dict[str, str], None)
        ),
        id2: PackageReference(
            provider="bar",
            configuration=cast(dict[str, str], None)
        )
    }

    json_value = JsonEncoder.encode(package_reference_map)

    with tempfile.NamedTemporaryFile(delete=True) as temp_file:
        config_folder_path = temp_file.name

    os.makedirs(config_folder_path)

    with open(os.path.join(config_folder_path, "packages.json"), "w") as file:
        json.dump(json_value, file)
        
    package_service = PackageService(config_folder_path)

    new_package_reference = PackageReference(
        provider="baz",
        configuration=cast(dict[str, str], None)
    )

    expected = package_reference_map.copy()
    expected[id1] = new_package_reference

    # Act
    success = await package_service.try_update(id1, new_package_reference)

    # Assert
    assert success

    with open(os.path.join(config_folder_path, "packages.json")) as json_file:
        actual = json.load(json_file)

    assert json.dumps(JsonEncoder.encode(actual)) == \
        json.dumps(JsonEncoder.encode(expected))

async def can_delete_package_test():

    # Arrange
    id1 = uuid.uuid4()
    id2 = uuid.uuid4()

    package_reference_map = {
        id1: PackageReference(
            provider="foo",
            configuration=cast(dict[str, str], None)
        ),
        id2: PackageReference(
            provider="bar",
            configuration=cast(dict[str, str], None)
        )
    }

    json_value = JsonEncoder.encode(package_reference_map)

    with tempfile.NamedTemporaryFile(delete=True) as temp_file:
        config_folder_path = temp_file.name

    os.makedirs(config_folder_path)

    packages_file_path = os.path.join(config_folder_path, "packages.json")

    with open(packages_file_path, "w") as file:
        json.dump(json_value, file)

    package_service = PackageService(config_folder_path)

    # Act
    await package_service.delete(id1)

    # Assert
    with open(packages_file_path, "r") as file:
        actual_package_reference_map = json.load(file)

    assert str(id1) not in actual_package_reference_map
    assert str(id2) in actual_package_reference_map

async def can_get_all_packages_test():

    # Arrange
    id1 = uuid.uuid4()
    id2 = uuid.uuid4()

    package_reference_map = {
        id1: PackageReference(
            provider="foo",
            configuration=cast(dict[str, str], None)
        ),
        id2: PackageReference(
            provider="bar",
            configuration=cast(dict[str, str], None)
        )
    }

    json_value = JsonEncoder.encode(package_reference_map)

    with tempfile.NamedTemporaryFile(delete=True) as temp_file:
        config_folder_path = temp_file.name

    os.makedirs(config_folder_path)

    packages_file_path = os.path.join(config_folder_path, "packages.json")

    with open(packages_file_path, "w") as file:
        json.dump(json_value, file)

    package_service = PackageService(config_folder_path)

    # Act
    actual_package_map = await package_service.get_all()

    # Assert
    expected = json.dumps(JsonEncoder.encode(package_reference_map))
    actual = json.dumps(JsonEncoder.encode(actual_package_map))

    assert actual == expected