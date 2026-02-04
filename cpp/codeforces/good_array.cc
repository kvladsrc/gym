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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;
  vector<int64_t> a(n);
  int64_t total = 0;
  map<int64_t, vector<int>> el_positions;
  for (int pos = 1; pos <= n; ++pos) {
    std::cin >> a[pos - 1];
    total += a[pos - 1];
    el_positions[a[pos - 1]].push_back(pos);
  }

  set<int> res;
  for (int pos = 1; pos <= n; ++pos) {
    auto i = a[pos - 1];
    auto need = total - i - i;
    if (need <= 0) {
      continue;
    }

    for (auto pos_other : el_positions[need]) {
      if (pos != pos_other) {
        res.insert(pos_other);
      }
    }
  }

  std::cout << res.size() << "\n";
  for (auto pos : res) {
    std::cout << pos << " ";
  }
  std::cout << "\n";

  return 0;
}
