"""The committed gRPC stubs match the proto, and the dependency pins match the stubs.

There is no CI configuration in this repo, so these checks live in the test suite: any
pipeline that runs the tests catches a proto edit that was not followed by regenerating the
stubs, a dependency floor that the stubs would refuse, and a SAM2 pin that drifted.
"""

from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

import pytest
from google.protobuf import descriptor_pb2

import segmentation_grpc
from segmentation_grpc import segmentation_pb2, segmentation_pb2_grpc

pytest.importorskip("grpc_tools")

_SERVER_ROOT = Path(__file__).resolve().parents[1]
_SEGMENTATION_ROOT = _SERVER_ROOT.parent
_REPO_ROOT = _SEGMENTATION_ROOT.parents[1]
_PROTO = _REPO_ROOT / "gRPC_Protos" / "Segmentation" / "SAM2" / "segmentation.proto"


def _descriptor_from_proto(tmp_path: Path) -> descriptor_pb2.FileDescriptorProto:
    out = tmp_path / "segmentation.desc"
    subprocess.check_call(
        [
            sys.executable, "-m", "grpc_tools.protoc",
            f"--proto_path={_PROTO.parent}",
            f"--descriptor_set_out={out}",
            str(_PROTO),
        ]
    )
    file_set = descriptor_pb2.FileDescriptorSet()
    file_set.ParseFromString(out.read_bytes())
    (file_proto,) = file_set.file
    return file_proto


def _drop_json_names(file_proto: descriptor_pb2.FileDescriptorProto) -> None:
    """protoc fills json_name in a descriptor set but leaves it out of generated Python."""

    def walk(message: descriptor_pb2.DescriptorProto) -> None:
        for field in message.field:
            field.ClearField("json_name")
        for nested in message.nested_type:
            walk(nested)

    for message in file_proto.message_type:
        walk(message)


@pytest.mark.skipif(not _PROTO.exists(), reason="shared proto is not checked out beside the server")
def test_committed_stubs_describe_exactly_the_shared_proto(tmp_path) -> None:
    from_proto = _descriptor_from_proto(tmp_path)
    from_stub = descriptor_pb2.FileDescriptorProto()
    segmentation_pb2.DESCRIPTOR.CopyToProto(from_stub)
    _drop_json_names(from_proto)
    _drop_json_names(from_stub)

    assert from_stub == from_proto, (
        "segmentation_pb2.py is out of date with gRPC_Protos/Segmentation/SAM2/segmentation.proto. "
        "Run: python -m segmentation_grpc"
    )


def test_every_rpc_in_the_descriptor_has_a_stub_and_a_servicer_method() -> None:
    service = segmentation_pb2.DESCRIPTOR.services_by_name["SegmentationService"]
    stub_methods = set(vars(segmentation_pb2_grpc.SegmentationServiceServicer))
    assert service.methods, "the service has no methods"
    for method in service.methods:
        assert method.name in stub_methods, f"{method.name} missing from the generated servicer"


def _stub_floors() -> tuple[tuple[int, int, int], tuple[int, int, int]]:
    """``(grpcio floor, protobuf floor)`` the generated code enforces at import."""
    grpc_source = Path(segmentation_pb2_grpc.__file__).read_text(encoding="utf-8")
    grpc_version = re.search(r"GRPC_GENERATED_VERSION = '(\d+)\.(\d+)\.(\d+)'", grpc_source)
    pb2_source = Path(segmentation_pb2.__file__).read_text(encoding="utf-8")
    protobuf_version = re.search(
        r"ValidateProtobufRuntimeVersion\(\s*_runtime_version\.Domain\.PUBLIC,\s*(\d+),\s*(\d+),\s*(\d+),",
        pb2_source,
    )
    assert grpc_version and protobuf_version, "could not read the versions the stubs enforce"
    return (
        tuple(int(part) for part in grpc_version.groups()),
        tuple(int(part) for part in protobuf_version.groups()),
    )


def _floor(text: str, package: str) -> tuple[int, ...]:
    match = re.search(rf"\"?{re.escape(package)}>=(\d+)\.(\d+)(?:\.(\d+))?", text)
    assert match, f"{package} has no floor in the requirements"
    return tuple(int(part or 0) for part in match.groups())


@pytest.mark.parametrize(
    "path",
    [
        _SERVER_ROOT / "pyproject.toml",
        _SERVER_ROOT / "requirements-docker.txt",
        _SEGMENTATION_ROOT / "segmentation_grpc" / "pyproject.toml",
    ],
    ids=lambda path: f"{path.parent.name}/{path.name}",
)
def test_dependency_floors_are_not_below_what_the_stubs_enforce(path: Path) -> None:
    grpc_floor, protobuf_floor = _stub_floors()
    text = path.read_text(encoding="utf-8")

    assert _floor(text, "grpcio") >= grpc_floor
    assert _floor(text, "grpcio-tools") >= grpc_floor
    assert _floor(text, "protobuf") >= protobuf_floor


def test_the_installed_runtime_satisfies_the_stub_floors() -> None:
    import google.protobuf
    import grpc

    grpc_floor, protobuf_floor = _stub_floors()
    installed_grpc = tuple(int(p) for p in grpc.__version__.split(".")[:3])
    installed_protobuf = tuple(int(p) for p in google.protobuf.__version__.split(".")[:3])

    assert installed_grpc >= grpc_floor
    assert installed_protobuf[0] == protobuf_floor[0] and installed_protobuf >= protobuf_floor


def test_the_sam2_commit_in_the_python_packages_matches_the_dockerfile() -> None:
    dockerfile = (_SEGMENTATION_ROOT / "Dockerfile").read_text(encoding="utf-8")
    pinned = re.search(r"ARG SAM2_GIT_SHA=([0-9a-f]{40})", dockerfile)
    assert pinned, "the Dockerfile no longer pins SAM2_GIT_SHA"

    for path in (_SERVER_ROOT / "pyproject.toml",):
        text = path.read_text(encoding="utf-8")
        shas = re.findall(r"facebookresearch/sam2/archive/([0-9a-f]{40})\.tar\.gz", text)
        assert shas == [pinned.group(1)], f"{path.name} pins SAM2 differently from the Dockerfile"


def test_the_package_exports_match_the_generated_module() -> None:
    assert hasattr(segmentation_grpc, "SegmentationServiceStub")


def test_each_package_is_described_by_pyproject_alone() -> None:
    """A setup.py beside pyproject.toml is a second place for dependency floors to drift."""
    assert not (_SERVER_ROOT / "setup.py").exists()
    assert not (_SEGMENTATION_ROOT / "segmentation_grpc" / "setup.py").exists()
    for pyproject in (_SERVER_ROOT / "pyproject.toml", _SEGMENTATION_ROOT / "segmentation_grpc" / "pyproject.toml"):
        assert "setuptools>=64" in pyproject.read_text(encoding="utf-8")  # editable installs from pyproject alone


def test_the_demo_page_files_are_packaged() -> None:
    text = (_SERVER_ROOT / "pyproject.toml").read_text(encoding="utf-8")
    assert "demo_static/*" in text
    assert (_SERVER_ROOT / "segmentation_server" / "demo_static" / "index.html").is_file()
