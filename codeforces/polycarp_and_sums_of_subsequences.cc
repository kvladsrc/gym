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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    vector<int> a(7);
    for (int i = 0; i < 7; ++i) {
      std::cin >> a[i];
    }
    sort(a.begin(), a.end());

    std::cout << a[0] << " ";
    std::cout << a[1] << " ";
    std::cout << a[6] - a[0] - a[1] << "\n";
  }

  return 0;
}
