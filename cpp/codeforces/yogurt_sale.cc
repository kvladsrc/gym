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
    int n;
    std::cin >> n;

    int a, b;
    std::cin >> a >> b;

    int case1 = a * n;
    int case2 = (n / 2) * b + ((n % 2) ? a : 0);
    std::cout << min(case1, case2) << "\n";
  }

  return 0;
}
