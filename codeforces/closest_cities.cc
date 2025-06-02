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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    size_t n;
    std::cin >> n;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    // Idea is to precalculate minimal costs for reaching any city
    // from left-most and right most.
    vector<int> left_to_right(n);
    vector<int> right_to_left(n);
    right_to_left[n - 1] = 0;
    right_to_left[n - 2] = 1;
    left_to_right[0] = 0;
    left_to_right[1] = 1;  // Second city is closest city from first.

    for (size_t idx = 2; idx < n; ++idx) {
      int cost;
      if (abs(a[idx - 2] - a[idx - 1]) > abs(a[idx] - a[idx - 1])) {
        cost = 1;
      } else {
        cost = abs(a[idx] - a[idx - 1]);
      }
      left_to_right[idx] = left_to_right[idx - 1] + cost;
    }

    for (int idx = n - 3; idx >= 0; --idx) {
      int cost;
      if (abs(a[idx + 2] - a[idx + 1]) > abs(a[idx] - a[idx + 1])) {
        cost = 1;
      } else {
        cost = abs(a[idx] - a[idx + 1]);
      }
      right_to_left[idx] = right_to_left[idx + 1] + cost;
    }

    int m;
    std::cin >> m;
    while (m--) {
      int f, s;
      std::cin >> f >> s;
      if (f > s) {
        int res = right_to_left[s - 1] - right_to_left[f - 1];
        std::cout << res << "\n";
      } else {
        int res = left_to_right[s - 1] - left_to_right[f - 1];
        std::cout << res << "\n";
      }
    }
  }

  return 0;
}
