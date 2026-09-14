using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.FocusDbContext;

public class FocusDbContext : DbContext
{
    public FocusDbContext(DbContextOptions<FocusDbContext> options) : base(options)
    {
    }


}
