#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

void merge_sort(vector<int> &a) {
  if (a.size() <= 1) {
    return;
  }

  size_t mid = (a.size() - 1) / 2;

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
  size_t left_idx = 0, right_idx = 0, i = 0;
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

int main(int argc, char *argv[]) {
  int n;
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
