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

int main(int argc, char *argv[]) {
  string a, b;
  std::cin >> a >> b;

  if (a != b) {
    std::cout << max(a.size(), b.size()) << "\n";
  } else {
    std::cout << -1 << "\n";
  }

  return 0;
}
