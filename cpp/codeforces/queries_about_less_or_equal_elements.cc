#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int binary_search(const vector<int>& a, int x) {
  if (x >= a.back()) return a.size() - 1;
  if (x < a.front()) return -1;

  int l = 0, r = a.size() - 1;

  int res = 0;
  while (l < r) {
    int mid = (l + r) / 2;

    if (a[mid] <= x) {
      res = mid;
      l = mid + 1;
    } else {
      r = mid;
    }
  }

  return res;
}

int main(int argc, char* argv[]) {
  int n, m;
  std::cin >> n >> m;

  vector<int> a(n), b(m);

  for (auto& i : a) {
    std::cin >> i;
  }

  for (auto& i : b) {
    std::cin >> i;
  }

  sort(a.begin(), a.end());

  for (auto i : b) {
    std::cout << binary_search(a, i) + 1 << " ";
  }

  return 0;
}
