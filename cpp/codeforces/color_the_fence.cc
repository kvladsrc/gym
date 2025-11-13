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
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int v = 0;
  std::cin >> v;

  vector<int> a(9);
  for (auto &i : a) {
    std::cin >> i;
  }

  // Just a greedy solution. We start by writing a largest number of
  // digits. It will be a set of cheapest digits. Then we trying to
  // increase each number from left to right.

  vector<int> res;
  int cheapest = 0;
  for (size_t d = 0; d < 9; ++d) {
    if (a[cheapest] >= a[d]) {
      cheapest = d;
    }
  }

  while (v >= a[cheapest]) {
    res.push_back(cheapest);
    v -= a[cheapest];
  }

  for (auto &rd : res) {
    for (int d = rd + 1; d < 9; ++d) {
      if (v + a[rd] - a[d] >= 0) {
        v = v + a[rd] - a[d];
        rd = d;
      }
    }
  }

  if (res.empty()) {
    std::cout << -1 << "\n";
    return 0;
  }

  for (auto d : res) {
    std::cout << d + 1;
  }
  std::cout << "\n";

  return 0;
}
