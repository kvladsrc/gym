#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
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

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    int s = 0;
    std::cin >> n >> s;

    vector<int> a(n);
    int minimal = INT_MAX;
    int maximal = INT_MIN;

    for (auto &i : a) {
      std::cin >> i;
      minimal = min(minimal, i);
      maximal = max(maximal, i);
    }

    if (s >= maximal) {
      std::cout << s - minimal << "\n";
      continue;
    }

    if (s <= minimal) {
      std::cout << maximal - s << "\n";
      continue;
    }

    std::cout << maximal - minimal + min(abs(minimal - s), abs(maximal - s))
              << "\n";
  }

  return 0;
}
