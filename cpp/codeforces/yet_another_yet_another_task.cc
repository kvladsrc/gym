#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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

  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  int res = INT_MIN;
  for (int val = -30; val <= 30; ++val) {
    int current = 0;
    int maximal = INT_MIN;
    for (auto i : a) {
      maximal = max(maximal, i);
      current += i;
      if ((current - maximal + val) < i) {
        current = i;
        maximal = i;
      }
      res = max(res, current - maximal);
    }
  }

  std::cout << res << "\n";

  return 0;
}
