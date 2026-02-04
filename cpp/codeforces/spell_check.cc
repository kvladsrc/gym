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
  string name = "Timur";
  sort(name.begin(), name.end());

  while (t--) {
    int n;
    std::cin >> n;
    string in;
    std::cin >> in;
    if (n != 5) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    sort(in.begin(), in.end());
    if (in == name) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
