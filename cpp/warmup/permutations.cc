#include "cpp/warmup/permutations.hpp"

#include <cstdint>
#include <utility>
#include <vector>

using std::vector;

namespace warmup {

// Heap's algorithm.
int64_t permutations_heaps(vector<int>& arr, int size) {
  if (size == 1) {
    return 1;
  }

  int64_t res = 0;
  for (int i = 0; i < size; i++) {
    res += permutations_heaps(arr, size - 1);
    // If size is odd, swap first and last element
    if (size % 2 == 1) {
      std::swap(arr[0], arr[size - 1]);
    } else {
      std::swap(arr[i], arr[size - 1]);
    }
  }

  return res;
}

}  // namespace warmup
