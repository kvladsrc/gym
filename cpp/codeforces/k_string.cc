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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int k = 0;
  std::cin >> k;

  string s;
  std::cin >> s;

  map<char, int> m;
  for (auto c : s) {
    m[c]++;
  }

  string res;
  bool done = true;
  for (auto p : m) {
    if ((p.second % k) != 0) {
      done = false;
      break;
    }

    for (int i = 0; i < (p.second / k); ++i) {
      res += p.first;
    }
  }

  if (done) {
    for (int i = 0; i < k; ++i) {
      std::cout << res;
    }
    std::cout << "\n";
  } else {
    std::cout << -1 << "\n";
  }

  return 0;
}
