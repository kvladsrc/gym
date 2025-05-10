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

  size_t n = 0;
  int m = 0;
  std::cin >> n >> m;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  set<int> rems;
  bool res = false;
  for (auto i : a) {
    set<int> tmp_rems(rems);
    for (auto r : tmp_rems) {
      rems.insert((r + i) % m);
    }
    rems.insert(i % m);
    if (rems.find(0) != rems.end()) {
      res = true;
      break;
    }
  }

  std::cout << (res ? "YES" : "NO") << "\n";

  return 0;
}
