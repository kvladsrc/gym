#include "cpp/warmup/merge_sort.hpp"

#include <cstddef>
#include <string>
#include <vector>

using std::string;
using std::vector;

namespace warmup {

void merge_sort(vector<int>& a) {
  if (a.size() <= 1) {
    return;
  }

  std::size_t const mid = (a.size() - 1) / 2;

  // Space complexity can be optimized by using buffer array.
  vector<int> left;
  for (std::size_t i = 0; i <= mid; ++i) {
    left.push_back(a[i]);
  }
  merge_sort(left);

  vector<int> right;
  for (std::size_t i = mid + 1; i < a.size(); ++i) {
    right.push_back(a[i]);
  }
  merge_sort(right);

  // Merge.
  std::size_t left_idx = 0;
  std::size_t right_idx = 0;
  std::size_t i = 0;
  while (left_idx < left.size() || right_idx < right.size()) {
    if (left_idx == left.size()) {
      a[i++] = right[right_idx++];
      continue;
    }

    if (right_idx == right.size()) {
      a[i++] = left[left_idx++];
      continue;
    }

    if (left[left_idx] < right[right_idx]) {
      a[i++] = left[left_idx++];
    } else {
      a[i++] = right[right_idx++];
    }
  }
}

}  // namespace warmup
