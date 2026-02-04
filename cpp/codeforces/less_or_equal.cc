#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int n, k;
  std::cin >> n >> k;
  vector<int> a(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> a[i];
  }

  sort(a.begin(), a.end());
  if (k > 0 && a[k] != a[k - 1]) {
    std::cout << a[k - 1] << "\n";
  } else {
    if (k == 0 && a[0] > 1) {
      std::cout << 1 << "\n";
    } else {
      std::cout << -1 << "\n";
    }
  }

  return 0;
}
