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

int partial(vector<int> &a) {
  if (a.empty()) {
    return -1;
  }

  auto pivot = a.back();
  int l = 0, r = a.size() - 2;
  while (l <= r) {
    if (a[l] < pivot) {
      l++;
      continue;
    }
    if (a[r] >= pivot) {
      r--;
      continue;
    }
    std::swap(a[l], a[r]);
  }

  std::swap(a[l], a.back());
  return l;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  auto pivot = partial(a);
  std::cout << pivot << endl;
  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << endl;

  return 0;
}
