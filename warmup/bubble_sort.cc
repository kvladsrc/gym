#include "cpp/warmup/bubble_sort.hpp"

#include <cstddef>
#include <iostream>
#include <utility>
#include <vector>

using std::vector;

namespace warmup {

void bubble_sort(vector<int> &a) {
  while (true) {
    bool swapped = false;

    for (std::size_t i = 1; i < a.size(); ++i) {
      if (a[i - 1] > a[i]) {
        std::swap(a[i - 1], a[i]);
        swapped = true;
      }
    }

    if (!swapped) {
      break;
    }
  }
}

}  // namespace warmup
