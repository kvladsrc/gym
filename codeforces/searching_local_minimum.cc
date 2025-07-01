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

int ask(int i, vector<int>& table) {
  if (i == 0 || i == static_cast<int>(table.size()) + 1) {
    return INT_MAX;
  }

  if (table[i - 1] != -1) {
    return table[i - 1];
  }

  std::cout << "? " << i << "\n";
  int res = 0;
  std::cin >> res;
  table[i - 1] = res;
  return res;
}

void ans(int i) {
  std::cout << "! " << i << "\n";
  std::cout.flush();
}

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  if (n == 1) {
    ans(1);
    return 0;
  }

  vector<int> table(n, -1);

  int l = 1;
  int r = n;
  int res = 0;

  while (l < r) {
    int mid = (l + r) / 2;
    auto am = ask(mid, table);
    auto left = ask(mid - 1, table);
    auto right = ask(mid + 1, table);

    if (am < left && am < right) {
      res = mid;
      break;
    }

    if (left < am) {
      r = mid - 1;
      res = mid - 1;
      continue;
    }

    l = mid + 1;
    res = mid + 1;
  }

  ans(res);

  return 0;
}
