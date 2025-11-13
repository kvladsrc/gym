#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

// O(1)
void print_coded(const string &a) {
  if (a.size() <= 10) {
    std::cout << a << "\n";
  } else {
    std::cout << a[0];
    std::cout << a.size() - 2;
    std::cout << a[a.size() - 1] << "\n";
  }
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<string> a(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> a[i];
  }

  for (const string &s : a) {
    print_coded(s);
  }

  return 0;
}
