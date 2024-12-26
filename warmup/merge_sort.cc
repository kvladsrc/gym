#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

static void merge_sort(vector<int> &a) {
  if (a.size() <= 1) {
    return;
  }

  size_t const mid = (a.size() - 1) / 2;

  // Space complexity can be optimized by using buffer array.
  vector<int> left;
  for (size_t i = 0; i <= mid; ++i) {
    left.push_back(a[i]);
  }
  merge_sort(left);

  vector<int> right;
  for (size_t i = mid + 1; i < a.size(); ++i) {
    right.push_back(a[i]);
  }
  merge_sort(right);

  // Merge.
  size_t left_idx = 0;
  size_t right_idx = 0;
  size_t i = 0;
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

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  merge_sort(a);

  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << "\n";

  return 0;
}
