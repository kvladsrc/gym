#include <cstdlib>
#include <exception>
#include <filesystem>
#include <iostream>
#include <string>

#include "mnist.h"  // NOLINT(build/include_subdir)

namespace {

std::string workspace_path(const char* value) {
  const std::filesystem::path path(value);
  const char* workspace = std::getenv("BUILD_WORKSPACE_DIRECTORY");
  if (path.is_relative() && workspace != nullptr) {
    return (std::filesystem::path(workspace) / path).string();
  }
  return path.string();
}

}  // namespace

int main(int argc, char* argv[]) {
  if (argc != 3) {
    std::cerr << "usage: " << argv[0] << " IMAGES LABELS\n";
    return 1;
  }

  try {
    const MnistDataset dataset =
        load_mnist(workspace_path(argv[1]), workspace_path(argv[2]));
    std::cout << dataset.images.size() << " images, " << dataset.rows << 'x'
              << dataset.cols << ", " << dataset.labels.size() << " labels\n";
  } catch (const std::exception& error) {
    std::cerr << error.what() << '\n';
    return 1;
  }
}
