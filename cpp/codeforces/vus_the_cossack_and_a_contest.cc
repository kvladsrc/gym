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
  int n, m, k;
  std::cin >> n >> m >> k;
  if (n > min(m, k)) {
    std::cout << "NO"
              << "\n";
  } else {
    std::cout << "YES"
              << "\n";
  }

  return 0;
}
