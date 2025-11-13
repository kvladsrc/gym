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

int ask(int x, int y) {
  std::cout << "? " << x << " " << y << "\n";
  int res = 0;
  std::cin >> res;
  return res;
}

void ans(const vector<int>& a) {
  std::cout << "! ";
  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << "\n";
  std::cout.flush();
}

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  vector<int> res(n, n);
  int l = 0;
  int r = n - 1;
  while (l < r) {
    auto c1 = ask(l + 1, r + 1);
    auto c2 = ask(r + 1, l + 1);
    if (c1 > c2) {
      res[l++] = c1;
    } else {
      res[r--] = c2;
    }
  }
  ans(res);

  return 0;
}
