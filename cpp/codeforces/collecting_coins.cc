#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
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
  int t;
  std::cin >> t;
  while (t--) {
    int a, b, c, n;
    std::cin >> a >> b >> c >> n;
    int total = a + b + c;
    int max_num = max(max(a, b), c);
    int to_eq = max_num * 3 - total;
    if (to_eq <= n && (n - to_eq) % 3 == 0)
      std::cout << "YES"
                << "\n";
    else
      std::cout << "NO"
                << "\n";
  }

  return 0;
}
