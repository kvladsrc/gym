#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  int m = 0;
  std::cin >> m;
  map<int, int> mp;
  for (int i = 0; i < m; ++i) {
    int buf = 0;
    std::cin >> buf;
    mp[buf]++;
  }

  int64_t l = 0;
  for (size_t i = 0; i < n; ++i) {
    l += (i + 1) * mp[a[i]];
  }

  int64_t r = 0;
  for (size_t i = 0; i < n; ++i) {
    r += (i + 1) * mp[a[n - 1 - i]];
  }

  std::cout << l << " " << r << "\n";

  return 0;
}
