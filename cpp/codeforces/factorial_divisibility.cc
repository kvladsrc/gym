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
  int n = 0;
  int x = 0;
  std::cin >> n >> x;

  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  vector<int> cnt(x + 1, 0);
  for (auto i : a) {
    cnt[i]++;
  }

  bool res = true;

  for (size_t idx = 1; idx < static_cast<size_t>(x); ++idx) {
    cnt[idx + 1] += cnt[idx] / static_cast<int>(idx + 1);
    cnt[idx] %= static_cast<int>(idx + 1);
    if (cnt[idx] != 0) {
      res = false;
      break;
    }
  }

  std::cout << (res ? "YES" : "NO") << "\n";

  return 0;
}
