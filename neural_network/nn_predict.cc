#include <algorithm>
#include <exception>
#include <iomanip>
#include <iostream>
#include <sstream>
#include <string>

#include "nn_io.h"   // NOLINT(build/include_subdir)
#include "nn_lib.h"  // NOLINT(build/include_subdir)

int main(int argc, char* argv[]) {
  if (argc != 2) {
    std::cerr << "usage: " << argv[0] << " MODEL\n";
    return 1;
  }

  try {
    NN net = NNIO::load(argv[1]);
    std::string line;

    while (std::getline(std::cin, line)) {
      std::istringstream values(line);
      V image;
      double pixel = 0.0;
      while (values >> pixel) {
        image.push_back(pixel);
      }

      const V probabilities = net.predict(image);
      for (std::size_t label = 0; label < probabilities.size(); ++label) {
        if (label != 0) {
          std::cout << ' ';
        }
        std::cout << std::setprecision(17) << probabilities[label];
      }
      std::cout << '\n' << std::flush;
    }
  } catch (const std::exception& error) {
    std::cerr << error.what() << '\n';
    return 1;
  }

  return 0;
}
