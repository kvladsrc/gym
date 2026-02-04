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
  int n, m;
  std::cin >> n >> m;
  bool bw = true;
  for (int i = 0; i < n * m; ++i) {
    if (!bw) continue;
    char c;
    std::cin >> c;
    if (c == 'C' || c == 'M' || c == 'Y') {
      bw = false;
    }
  }

  if (bw)
    std::cout << "#Black&White"
              << "\n";
  else
    std::cout << "#Color"
              << "\n";

  return 0;
}
