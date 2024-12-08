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
using std::gcd;
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

int upper_bound(vector<int> &a, int i) {
  int l = 0, r = a.size() - 1;
  int res = -1;

  while (l <= r) {
    int mid = (l + r) / 2;
    if (a[mid] >= i) {
      r = mid - 1;
      res = mid;
    } else {
      l = mid + 1;
    }
  }

  return res;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  int el;
  std::cin >> el;
  std::cout << upper_bound(a, el) << "\n";

  return 0;
}
